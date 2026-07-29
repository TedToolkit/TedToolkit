using System.Diagnostics;
using System.Text;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Versioning;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Execution;
using TedToolkit.ModularPipelines.Combine.Models;

using TedToolkit.Build.ReleaseLifecycle;

Console.OutputEncoding = Encoding.UTF8;

var root = new DirectoryInfo(Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")));
var output = new DirectoryInfo(Path.Combine(root.FullName, "output"));
var solution = new FileInfo(Path.Combine(root.FullName, "TedToolkit.slnx"));
var sourceRevision = Environment.GetEnvironmentVariable("GITHUB_SHA")
                     ?? Environment.GetEnvironmentVariable("CI_COMMIT_SHA")
                     ?? await ReadGitRevisionAsync(root);

if (args is ["resolve-version"])
{
    var resolvedVersion = await new RepositoryReleaseVersionResolver(root)
        .ResolveAsync(
            DateOnly.FromDateTime(DateTime.Now),
            sourceRevision,
            new FileInfo(Path.Combine(
                root.FullName,
                "Build",
                "release-history.v1.json")),
            CancellationToken.None)
        .ConfigureAwait(false);
    var versionOutputFile = Environment.GetEnvironmentVariable(
                                "TEDTOOLKIT_VERSION_OUTPUT_FILE")
                            ?? ReadRequiredEnvironmentVariable(
                                "GITHUB_OUTPUT");
    await File.AppendAllTextAsync(
            versionOutputFile,
            $"version={resolvedVersion}{Environment.NewLine}")
        .ConfigureAwait(false);
    return;
}

if (args is ["recover-release"])
{
    var action = ReadRequiredEnvironmentVariable(
        "TEDTOOLKIT_RECOVERY_ACTION");
    var version = ReadRequiredEnvironmentVariable(
        "TEDTOOLKIT_RECOVERY_VERSION");
    var auditReference = ReadRequiredEnvironmentVariable(
        "TEDTOOLKIT_AUDIT_REFERENCE");
    var recoveryRunIdentity =
        $"github:{ReadRequiredEnvironmentVariable("GITHUB_RUN_ID")}";
    var recovery = new ReleaseRecoveryCoordinator(root);
    string disposition;

    if (action.Equals(
            "AbandonPublication",
            StringComparison.Ordinal))
    {
        disposition = await recovery.AbandonAsync(
                version,
                auditReference,
                CancellationToken.None)
            .ConfigureAwait(false);
    }
    else if (action.Equals(
                 "FinalizeRelease",
                 StringComparison.Ordinal))
    {
        disposition = await recovery.FinalizeAsync(
                version,
                auditReference,
                recoveryRunIdentity,
                async (targetRevision, cancellationToken) =>
                {
                    _ = await ReleaseFinalization.FinalizeAsync(
                            new()
                            {
                                Provider = RepositoryProviderKind.GitHub,
                                GitHub = CreateGitHubOptions(),
                                Request = new ReleasePublicationRequest
                                {
                                    Version = version,
                                    TagName = version,
                                    TargetRevision = targetRevision,
                                    Title = Environment.GetEnvironmentVariable(
                                                "TEDTOOLKIT_RELEASE_TITLE")
                                            ?? $"TedToolkit {version}",
                                    Body = Environment.GetEnvironmentVariable(
                                               "TEDTOOLKIT_RELEASE_BODY")
                                           ?? "",
                                    Artifacts = [],
                                },
                            },
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                },
                CancellationToken.None)
            .ConfigureAwait(false);
    }
    else
    {
        throw new InvalidDataException(
            "TEDTOOLKIT_RECOVERY_ACTION is invalid.");
    }

    await AppendRecoverySummaryAsync(
            action,
            version,
            auditReference,
            recoveryRunIdentity,
            disposition)
        .ConfigureAwait(false);
    return;
}

var packageVersion = Environment.GetEnvironmentVariable(
    "TEDTOOLKIT_PACKAGE_VERSION");
var inputMode = ParseEnum(
    "TEDTOOLKIT_PIPELINE_INPUT_MODE",
    PipelineInputMode.RunBuild);
var manualProfile = ParseOptionalEnum<PipelineProfile>(
    "TEDTOOLKIT_PIPELINE_PROFILE");
var actions = new PipelineActionOptions
{
    PushNuGetPackages = ReadBoolean("TEDTOOLKIT_PUSH_NUGET"),
    PublishArtifacts = ReadBoolean("TEDTOOLKIT_PUBLISH_ARTIFACTS"),
    CreateChangeRequest = ReadBoolean("TEDTOOLKIT_CREATE_CHANGE_REQUEST"),
    CreateRelease = ReadBoolean("TEDTOOLKIT_CREATE_RELEASE"),
    EmitNotifications = ReadBoolean("TEDTOOLKIT_EMIT_NOTIFICATIONS"),
};
var provider = ParseOptionalEnum<RepositoryProviderKind>(
    "TEDTOOLKIT_REPOSITORY_PROVIDER");
var checkpoint = actions.PushNuGetPackages
                 && ReadBoolean("TEDTOOLKIT_USE_GIT_TAG_CHECKPOINT")
    ? new GitTagPackagePublicationCheckpoint(
        root,
        $"github:{ReadRequiredEnvironmentVariable("GITHUB_RUN_ID")}")
    : null;

if (checkpoint is not null
    && await checkpoint.PrepareAsync(
            packageVersion
            ?? throw new InvalidDataException(
                "TEDTOOLKIT_PACKAGE_VERSION is required for checkpointed publication."),
            CancellationToken.None)
        .ConfigureAwait(false))
{
    return;
}

var buildInputs = inputMode == PipelineInputMode.RunBuild
    ? CreateBuildInputs(root, output, solution, sourceRevision, packageVersion)
    : null;
var combineOptions = new CombineOptions
{
    ExecutionPolicy = manualProfile is null
        ? PipelineExecutionPolicy.Standard
        : PipelineExecutionPolicy.Manual,
    ManualProfile = manualProfile,
    InputMode = inputMode,
    ArtifactManifestPath = inputMode == PipelineInputMode.ConsumeArtifacts
        ? Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_ARTIFACT_MANIFEST")
          ?? "output/pipeline-artifacts.v1.json"
        : null,
    Actions = actions,
    NuGetPush = actions.PushNuGetPackages
        ? CreateNuGetOptions(checkpoint is not null)
        : null,
    PackagePublicationCheckpoint = checkpoint,
    RepositoryProvider = provider,
    GitHub = provider == RepositoryProviderKind.GitHub
        ? CreateGitHubOptions()
        : null,
    GitLab = provider == RepositoryProviderKind.GitLab
        ? CreateGitLabOptions()
        : null,
    Publication = actions.PublishArtifacts || actions.CreateRelease
        ? CreatePublication(packageVersion)
        : null,
};
var builder = Pipeline.CreateBuilder();
builder.AddStandardPipeline(
    root,
    combineOptions,
    buildInputs,
    new BuildOptions
    {
        RunFormat = ReadBoolean(
            "TEDTOOLKIT_RUN_FORMAT",
            defaultValue: true),
    });
await builder.ExecutePipelineAsync().ConfigureAwait(false);

if (checkpoint is not null)
{
    await checkpoint.CleanupPendingAsync(CancellationToken.None)
        .ConfigureAwait(false);
}

static BuildInputs CreateBuildInputs(
    DirectoryInfo root,
    DirectoryInfo output,
    FileInfo solution,
    string sourceRevision,
    string? packageVersion)
{
    return new()
    {
        RootDirectory = root,
        ArtifactRootDirectory = output,
        SourceRevision = sourceRevision,
        MsBuildVersion = new TemporaryMsBuildVersionOptions
        {
            TargetFile = new FileInfo(Path.Combine(
                root.FullName,
                "Directory.Build.props")),
            TargetVersion = packageVersion,
            RecoveryFile = new FileInfo(Path.Combine(
                root.FullName,
                ".tedtoolkit-version-recovery.json")),
        },
        FormatTargets =
        [
            new()
            {
                File = solution,
                Scope = FormatScope.WorkingTreeTracked,
            },
        ],
        BuildTargets =
        [
            new()
            {
                Name = "TedToolkit",
                File = solution,
                Configuration = "Release",
                CleanBeforeBuild = true,
            },
        ],
        TestTargets = [],
        PackTargets = CreatePackTargets(root, packageVersion),
        PublishTargets = [],
    };
}

static IReadOnlyList<PackTarget> CreatePackTargets(
    DirectoryInfo root,
    string? packageVersion)
{
    if (string.IsNullOrWhiteSpace(packageVersion))
    {
        return [];
    }

    return
    [
        CreatePackTarget(
            root,
            "CodeAnalysis",
            "TedToolkit.CodeAnalysis",
            packageVersion),
        CreatePackTarget(
            root,
            "ModularPipelines.Build",
            "TedToolkit.ModularPipelines.Build",
            packageVersion),
        CreatePackTarget(
            root,
            "ModularPipelines.Combine",
            "TedToolkit.ModularPipelines.Combine",
            packageVersion),
    ];
}

static PackTarget CreatePackTarget(
    DirectoryInfo root,
    string name,
    string projectName,
    string packageVersion)
{
    return new()
    {
        Name = name,
        File = new FileInfo(Path.Combine(
            root.FullName,
            projectName,
            $"{projectName}.csproj")),
        PackageVersion = packageVersion,
    };
}

static NuGetPushOptions CreateNuGetOptions(bool useCheckpoint)
{
    return new()
    {
        Source = ReadAbsoluteUri("TEDTOOLKIT_NUGET_SOURCE"),
        SymbolSource = ReadOptionalAbsoluteUri(
            "TEDTOOLKIT_NUGET_SYMBOL_SOURCE"),
        AuthenticationMode = ParseEnum(
            "TEDTOOLKIT_NUGET_AUTH_MODE",
            NuGetAuthenticationMode.ApiKey),
        CredentialReference = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_NUGET_CREDENTIAL_REFERENCE")
            ?? "NUGET_API_KEY",
        SymbolCredentialReference = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_NUGET_SYMBOL_CREDENTIAL_REFERENCE"),
        ConfigFilePath = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_NUGET_CONFIG_FILE"),
        SkipDuplicate = false,
        UsePackagePublicationCheckpoint = useCheckpoint,
    };
}

static GitHubConnectionOptions CreateGitHubOptions()
{
    return new()
    {
        InstanceUrl = ReadOptionalAbsoluteUri(
            "TEDTOOLKIT_GITHUB_INSTANCE_URL"),
        ApiUrl = ReadOptionalAbsoluteUri("TEDTOOLKIT_GITHUB_API_URL"),
        Owner = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_GITHUB_OWNER"),
        Repository = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_GITHUB_REPOSITORY"),
        AuthenticationMode = ParseEnum(
            "TEDTOOLKIT_GITHUB_AUTH_MODE",
            GitHubAuthenticationMode.ActionsToken),
        CredentialReference = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_GITHUB_CREDENTIAL_REFERENCE"),
    };
}

static GitLabConnectionOptions CreateGitLabOptions()
{
    var projectIdText = Environment.GetEnvironmentVariable(
        "TEDTOOLKIT_GITLAB_PROJECT_ID");
    return new()
    {
        InstanceUrl = ReadOptionalAbsoluteUri(
            "TEDTOOLKIT_GITLAB_INSTANCE_URL"),
        ApiUrl = ReadOptionalAbsoluteUri("TEDTOOLKIT_GITLAB_API_URL"),
        ProjectId = long.TryParse(
            projectIdText,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var projectId)
            ? projectId
            : null,
        AuthenticationMode = ParseEnum(
            "TEDTOOLKIT_GITLAB_AUTH_MODE",
            GitLabAuthenticationMode.JobToken),
        CredentialReference = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_GITLAB_CREDENTIAL_REFERENCE"),
    };
}

static PublicationOptions CreatePublication(string? packageVersion)
{
    return new()
    {
        Version = packageVersion
                  ?? throw new InvalidDataException(
                      "TEDTOOLKIT_PACKAGE_VERSION is required for publication."),
        TagName = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_RELEASE_TAG"),
        Title = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_RELEASE_TITLE"),
        Body = Environment.GetEnvironmentVariable(
            "TEDTOOLKIT_RELEASE_BODY") ?? "",
    };
}

static async Task<string> ReadGitRevisionAsync(DirectoryInfo root)
{
    using var process = new Process
    {
        StartInfo = new("git")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        },
    };
    process.StartInfo.ArgumentList.Add("rev-parse");
    process.StartInfo.ArgumentList.Add("--verify");
    process.StartInfo.ArgumentList.Add("HEAD");

    if (!process.Start())
    {
        throw new InvalidOperationException(
            "Unable to start git for source revision resolution.");
    }

    var revision = await process.StandardOutput.ReadToEndAsync()
        .ConfigureAwait(false);
    await process.WaitForExitAsync().ConfigureAwait(false);

    if (process.ExitCode != 0
        || string.IsNullOrWhiteSpace(revision))
    {
        throw new InvalidDataException(
            "Unable to resolve the source revision.");
    }

    return revision.Trim();
}

static bool ReadBoolean(string name, bool defaultValue = false)
{
    var value = Environment.GetEnvironmentVariable(name);
    return value is null
        ? defaultValue
        : bool.TryParse(value, out var parsed)
          && parsed;
}

static T ParseEnum<T>(string name, T defaultValue)
    where T : struct, Enum
{
    var value = Environment.GetEnvironmentVariable(name);
    return value is null
        ? defaultValue
        : Enum.Parse<T>(value, ignoreCase: true);
}

static T? ParseOptionalEnum<T>(string name)
    where T : struct, Enum
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value)
        ? null
        : Enum.Parse<T>(value, ignoreCase: true);
}

static Uri ReadAbsoluteUri(string name)
{
    return ReadOptionalAbsoluteUri(name)
           ?? throw new InvalidDataException($"{name} is required.");
}

static Uri? ReadOptionalAbsoluteUri(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value)
        ? null
        : new Uri(value, UriKind.Absolute);
}

static string ReadRequiredEnvironmentVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    if (!string.IsNullOrWhiteSpace(value)
        && !value.Any(char.IsControl))
    {
        return value;
    }

    throw new InvalidDataException($"{name} is required.");
}

static Task AppendRecoverySummaryAsync(
    string action,
    string version,
    string auditReference,
    string recoveryRunIdentity,
    string disposition)
{
    var summaryFile = ReadRequiredEnvironmentVariable(
        "GITHUB_STEP_SUMMARY");
    var summary = string.Join(
        Environment.NewLine,
        "### TedToolkit release recovery",
        "",
        $"action: {action}",
        $"version: {version}",
        $"audit-reference: {auditReference}",
        $"recovery-run-identity: {recoveryRunIdentity}",
        $"disposition: {disposition}",
        "");
    return File.AppendAllTextAsync(summaryFile, summary);
}
