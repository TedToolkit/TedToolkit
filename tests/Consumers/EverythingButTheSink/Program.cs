using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Events;
using TedToolkit.ModularPipelines.Combine.Execution;
using TedToolkit.ModularPipelines.Combine.Modules;

var mode = args.SingleOrDefault()
    ?? throw new InvalidDataException("Specify produce or consume.");
var fixtureRoot = new DirectoryInfo(Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));

if (mode.Equals("produce", StringComparison.Ordinal))
{
    await VerifyOptionalDescriptionAdapterAsync(fixtureRoot);
    await ProduceAsync(fixtureRoot);
    return;
}

if (mode.Equals("consume", StringComparison.Ordinal))
{
    await ConsumeAsync(fixtureRoot);
    return;
}

throw new InvalidDataException($"Unknown compatibility mode '{mode}'.");

static async Task VerifyOptionalDescriptionAdapterAsync(DirectoryInfo root)
{
    var marker = new FileInfo(Path.Combine(root.FullName, "adapter-probe.txt"));
    await File.WriteAllTextAsync(marker.FullName, "neutral adapter probe");

    try
    {
        await RunLocalBuildAsync(root, generator: null);
        var generator = new RecordingDescriptionGenerator();
        await RunLocalBuildAsync(root, generator);
        CompatibilityAssert.Require(generator.Requests.Count == 1,
            "The optional description adapter did not receive exactly one request.");
        var request = generator.Requests.Single();
        CompatibilityAssert.Require(
            request.Kind == ChangeDescriptionKind.CommitMessage
            && request.SourceRevision
            == CompatibilityConstants.SourceRevision
            && request.TargetRevision is null
            && request.ChangedPaths.Count > 0
            && request.Instructions.Length > 0,
            "The description adapter did not receive the bounded neutral model.");
    }
    finally
    {
        marker.Delete();
    }
}

static async Task RunLocalBuildAsync(
    DirectoryInfo root,
    RecordingDescriptionGenerator? generator)
{
    var builder = Pipeline.CreateBuilder();

    if (generator is not null)
    {
        builder.ConfigureServices((_, services) =>
            services.AddSingleton<IChangeDescriptionGenerator>(generator));
    }

    builder.AddStandardPipeline(
        root,
        new()
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = PipelineProfile.LocalBuild,
            ExecutionContext = new()
            {
                Trigger = PipelineTriggerKind.Local,
                IsCi = false,
            },
            ArtifactValidation = new()
            {
                ExpectedSourceRevision =
                    CompatibilityConstants.SourceRevision,
            },
        },
        CreateBuildInputs(
            root,
            new DirectoryInfo(Path.Combine(root.FullName, "adapter-output")),
            includeProducers: false),
        new()
        {
            RunFormat = false,
            GenerateChangeDescriptions = true,
        });
    await builder.ExecutePipelineAsync().ConfigureAwait(false);
}

static async Task ProduceAsync(DirectoryInfo root)
{
    var builder = Pipeline.CreateBuilder();
    builder.AddStandardPipeline(
        root,
        new()
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = PipelineProfile.Publish,
            ExecutionContext = new()
            {
                Trigger = PipelineTriggerKind.Manual,
                IsCi = true,
            },
            ArtifactValidation = new()
            {
                ExpectedSourceRevision =
                    CompatibilityConstants.SourceRevision,
            },
        },
        CreateBuildInputs(
            root,
            new DirectoryInfo(Path.Combine(root.FullName, "output")),
            includeProducers: true),
        new()
        {
            RunFormat = false,
        });
    await builder.ExecutePipelineAsync().ConfigureAwait(false);
}

static BuildInputs CreateBuildInputs(
    DirectoryInfo root,
    DirectoryInfo artifactRoot,
    bool includeProducers)
{
    var app = new FileInfo(Path.Combine(
        root.FullName,
        "FixtureApp",
        "FixtureApp.csproj"));
    var tests = new FileInfo(Path.Combine(
        root.FullName,
        "FixtureApp.Tests",
        "FixtureApp.Tests.csproj"));
    return new()
    {
        RootDirectory = root,
        ArtifactRootDirectory = artifactRoot,
        SourceRevision = CompatibilityConstants.SourceRevision,
        MsBuildVersion = new()
        {
            TargetFile = new(Path.Combine(root.FullName, "fixture.props")),
            RecoveryFile = new(Path.Combine(
                root.FullName,
                ".compatibility-version.recovery.json")),
            TargetVersion = includeProducers
                ? CompatibilityConstants.FixtureVersion
                : null,
        },
        BuildTargets = includeProducers
            ?
            [
                new()
                {
                    Name = "fixture-app",
                    File = app,
                    Arguments = ["--no-restore",],
                },
            ]
            : [],
        TestTargets = includeProducers
            ?
            [
                new()
                {
                    Name = "fixture-tests",
                    File = tests,
                    Command = DotNetTestCommand.MicrosoftTestingPlatformTest,
                    CommandArguments = ["--no-restore",],
                },
            ]
            : [],
        PackTargets = includeProducers
            ?
            [
                new()
                {
                    Name = "fixture-package",
                    File = app,
                    PackageVersion =
                        CompatibilityConstants.FixtureVersion,
                    Arguments = ["--no-restore", "--no-build",],
                },
            ]
            : [],
        PublishTargets = includeProducers
            ?
            [
                CreatePublishTarget(app, "fixture-linux", "linux-x64"),
                CreatePublishTarget(app, "fixture-windows", "win-x64"),
            ]
            : [],
    };
}

static DotnetPublishTarget CreatePublishTarget(
    FileInfo app,
    string name,
    string runtimeIdentifier)
{
    return new()
    {
        Name = name,
        ArtifactName = name,
        File = app,
        Framework = "net10.0",
        RuntimeIdentifier = runtimeIdentifier,
        SelfContained = false,
        Arguments = ["--no-restore",],
    };
}

static async Task ConsumeAsync(DirectoryInfo producerRoot)
{
    var isolated = new DirectoryInfo(Path.Combine(
        producerRoot.FullName,
        "isolated",
        "output"));
    CopyDirectory(
        new DirectoryInfo(Path.Combine(producerRoot.FullName, "output")),
        isolated);
    var root = isolated.Parent
        ?? throw new InvalidOperationException("The isolated root is invalid.");
    var manifestPath = new FileInfo(Path.Combine(
        isolated.FullName,
        "pipeline-artifacts.v1.json"));
    var manifest = await new PipelineArtifactManifestStore().ReadAsync(
        root,
        manifestPath,
        new()
        {
            ExpectedSourceRevision =
                CompatibilityConstants.SourceRevision,
        });
    CompatibilityAssert.Require(
        manifest.Status == PipelineRunStatus.Succeeded
        && manifest.SourceRevision
        == CompatibilityConstants.SourceRevision
        && manifest.PackageVersion
        == CompatibilityConstants.FixtureVersion
        && manifest.Tests.Count == 1
        && manifest.Tests.Single().Passed == 1,
        "The copied typed manifest lost source or test semantics.");
    var archives = manifest.Artifacts
        .Where(artifact =>
            artifact.Kind == PipelineArtifactKind.PublishArchive)
        .ToArray();
    CompatibilityAssert.Require(
        archives.Length == 2
        && archives.Select(artifact => artifact.RuntimeIdentifier)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(["linux-x64", "win-x64",]),
        "The copied manifest lost the distinct multi-RID archives.");

    await using var server = new FakeGitLabServer();
    await server.StartAsync();
    var originalJobToken = Environment.GetEnvironmentVariable("CI_JOB_TOKEN");
    var originalGitLabCi = Environment.GetEnvironmentVariable("GITLAB_CI");
    var originalRefName = Environment.GetEnvironmentVariable(
        "CI_COMMIT_REF_NAME");
    var originalCommitSha = Environment.GetEnvironmentVariable(
        "CI_COMMIT_SHA");
    var boundaryDirectory = new DirectoryInfo(Path.Combine(
        root.FullName,
        "temp"));
    boundaryDirectory.Create();
    var fakeGitLabLog = Path.Combine(
        boundaryDirectory.FullName,
        "fake-gitlab.log");
    await File.WriteAllTextAsync(fakeGitLabLog, string.Empty);
    Environment.SetEnvironmentVariable(
        "COMPATIBILITY_FAKE_GITLAB_LOG",
        fakeGitLabLog);
    Environment.SetEnvironmentVariable("CI_JOB_TOKEN", "synthetic-token");
    Environment.SetEnvironmentVariable("GITLAB_CI", "true");
    Environment.SetEnvironmentVariable(
        "CI_COMMIT_REF_NAME",
        "feature/compatibility");
    Environment.SetEnvironmentVariable(
        "CI_COMMIT_SHA",
        CompatibilityConstants.SourceRevision);
    var nugetConfig = Path.Combine(
        boundaryDirectory.FullName,
        "fake-nuget.config");
    await File.WriteAllTextAsync(
        nugetConfig,
        $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="synthetic"
                 value="{{server.NuGetSource}}"
                 allowInsecureConnections="true" />
          </packageSources>
        </configuration>
        """);

    try
    {
        await RunConsumerProfileAsync(
            root,
            server.ApiBase,
            server.NuGetSource,
            PipelineProfile.Message,
            withAdapters: false);
        await RunConsumerProfileAsync(
            root,
            server.ApiBase,
            server.NuGetSource,
            PipelineProfile.Publish,
            withAdapters: false);

        var messageEvents = await RunConsumerProfileAsync(
            root,
            server.ApiBase,
            server.NuGetSource,
            PipelineProfile.Message,
            withAdapters: true);
        CompatibilityAssert.Require(
            messageEvents.SequenceEqual(
            [
                nameof(PipelineEventKind.BuildValidated),
                nameof(PipelineEventKind.ChangeRequestCompleted),
                nameof(PipelineEventKind.PipelineCompleted),
                "extension-after-combine",
            ]),
            "Message event/extension ordering changed.");

        var publishEvents = await RunConsumerProfileAsync(
            root,
            server.ApiBase,
            server.NuGetSource,
            PipelineProfile.Publish,
            withAdapters: true);
        CompatibilityAssert.Require(
            publishEvents.SequenceEqual(
            [
                nameof(PipelineEventKind.BuildValidated),
                nameof(PipelineEventKind.PublicationCompleted),
                nameof(PipelineEventKind.ReleaseCompleted),
                nameof(PipelineEventKind.PipelineCompleted),
                "extension-after-combine",
            ]),
            "Publish event/extension ordering changed.");

        server.AssertExpectedOperations();
    }
    finally
    {
        Environment.SetEnvironmentVariable(
            "COMPATIBILITY_FAKE_GITLAB_LOG",
            null);
        Environment.SetEnvironmentVariable("CI_JOB_TOKEN", originalJobToken);
        Environment.SetEnvironmentVariable("GITLAB_CI", originalGitLabCi);
        Environment.SetEnvironmentVariable(
            "CI_COMMIT_REF_NAME",
            originalRefName);
        Environment.SetEnvironmentVariable(
            "CI_COMMIT_SHA",
            originalCommitSha);
    }
}

static async Task<IReadOnlyList<string>> RunConsumerProfileAsync(
    DirectoryInfo root,
    Uri apiBase,
    Uri nugetSource,
    PipelineProfile profile,
    bool withAdapters)
{
    var events = new List<string>();
    CompatibilityRecorder.Events = events;
    var builder = Pipeline.CreateBuilder();

    if (withAdapters)
    {
        builder.ConfigureServices((_, services) =>
            services.AddSingleton<IPipelineEventSink>(
                new RecordingEventSink(events)));
    }

    var actions = profile == PipelineProfile.Message
        ? new PipelineActionOptions
        {
            CreateChangeRequest = true,
            EmitNotifications = withAdapters,
        }
        : new()
        {
            PushNuGetPackages = true,
            PublishArtifacts = true,
            CreateRelease = true,
            EmitNotifications = withAdapters,
        };
    builder.AddStandardPipeline(
        root,
        new()
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = profile,
            InputMode = PipelineInputMode.ConsumeArtifacts,
            ArtifactManifestPath = "output/pipeline-artifacts.v1.json",
            ExecutionContext = new()
            {
                Trigger = PipelineTriggerKind.Manual,
                IsCi = true,
                Branch = "feature/compatibility",
                SourceRevision = CompatibilityConstants.SourceRevision,
            },
            ArtifactValidation = new()
            {
                ExpectedSourceRevision =
                    CompatibilityConstants.SourceRevision,
            },
            Actions = actions,
            RepositoryProvider = RepositoryProviderKind.GitLab,
            GitLab = new()
            {
                InstanceUrl = new(apiBase.GetLeftPart(UriPartial.Authority)),
                ApiUrl = apiBase,
                ProjectId = 42,
                AuthenticationMode = GitLabAuthenticationMode.JobToken,
                AllowInsecureHttp = true,
            },
            NuGetPush = profile == PipelineProfile.Publish
                ? new()
                {
                    Source = nugetSource,
                    AuthenticationMode = NuGetAuthenticationMode.NuGetConfig,
                    ConfigFilePath = "temp/fake-nuget.config",
                    AllowInsecureHttp = true,
                }
                : null,
            Publication = profile == PipelineProfile.Publish
                ? new()
                {
                    Version = CompatibilityConstants.FixtureVersion,
                    TagName = CompatibilityConstants.FixtureVersion,
                    Title =
                        $"Compatibility {CompatibilityConstants.FixtureVersion}",
                    Body = "Synthetic provider-neutral release.",
                }
                : null,
        },
        moduleRegistrations:
        [
            new(
                new HashSet<PipelineProfile> { profile },
                PipelineExtensionKind.CombineHook,
                WritesRepositoryFiles: false,
                pipeline => pipeline.AddModule<ExtensionProbeModule>()),
        ]);
    await builder.ExecutePipelineAsync().ConfigureAwait(false);
    return events;
}

static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
{
    CompatibilityAssert.Require(
        source.Exists,
        "The producer artifact root is missing.");

    if (destination.Exists)
    {
        destination.Delete(recursive: true);
    }

    destination.Create();

    foreach (var file in source.EnumerateFiles(
                 "*",
                 SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source.FullName, file.FullName);
        var target = new FileInfo(Path.Combine(
            destination.FullName,
            relative));
        target.Directory!.Create();
        file.CopyTo(target.FullName, overwrite: true);
    }
}

internal sealed class RecordingDescriptionGenerator
    : IChangeDescriptionGenerator
{
    public List<ChangeDescriptionRequest> Requests { get; } = [];

    public Task<ChangeDescription?> GenerateAsync(
        ChangeDescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult<ChangeDescription?>(new()
        {
            Subject = "Synthetic neutral description",
            Body = "Generated by a consumer-owned fake.",
        });
    }
}

internal sealed class RecordingEventSink(List<string> events)
    : IPipelineEventSink
{
    public Task PublishAsync(
        PipelineEvent pipelineEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityAssert.Require(
            pipelineEvent.RunResult.BuildResult?.Artifacts.Count > 0
            && pipelineEvent.RunResult.BuildResult.Tests.Count == 1,
            "The notification adapter did not receive a neutral typed run.");

        if (pipelineEvent.Kind == PipelineEventKind.ChangeRequestCompleted)
        {
            CompatibilityAssert.Require(
                pipelineEvent.ChangeRequest is not null
                && pipelineEvent.ChangeRequest.SourceBranch
                == "feature/compatibility",
                "The Message event lost the neutral change request.");
        }

        if (pipelineEvent.Kind == PipelineEventKind.ReleaseCompleted)
        {
            CompatibilityAssert.Require(
                pipelineEvent.Release?.TagName
                == CompatibilityConstants.FixtureVersion,
                "The Publish event lost the neutral release.");
        }

        events.Add(pipelineEvent.Kind.ToString());
        return Task.CompletedTask;
    }
}

internal static class CompatibilityRecorder
{
    public static List<string> Events { get; set; } = [];
}

internal static class CompatibilityConstants
{
    public const string FixtureVersion = "2026.7.29.4";

    public const string SourceRevision =
        "dddddddddddddddddddddddddddddddddddddddd";
}

internal static class CompatibilityAssert
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}

/// <summary>
/// Records that a trusted consumer hook ran after artifact consumption.
/// </summary>
[DependsOn<ConsumeArtifactsCombineModule>]
public sealed class ExtensionProbeModule
    : ReleaseStageModule<string>
{
    /// <inheritdoc/>
    protected override Task<string?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityRecorder.Events.Add("extension-after-combine");
        return Task.FromResult<string?>("recorded");
    }
}

internal sealed class FakeGitLabServer : IAsyncDisposable
{
    private readonly TcpListener _listener =
        new(IPAddress.Loopback, 0);

    private readonly CancellationTokenSource _stopping = new();

    private readonly List<string> _requests = [];

    private Task? _loop;

    public Uri ApiBase { get; private set; } =
        new("http://127.0.0.1/");

    public Uri NuGetSource { get; private set; } =
        new("http://127.0.0.1/v3/index.json");

    public Task StartAsync()
    {
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        var authority = $"http://127.0.0.1:{endpoint.Port}";
        ApiBase = new($"{authority}/api/v4/");
        NuGetSource = new($"{authority}/v3/index.json");
        _loop = AcceptAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public void AssertExpectedOperations()
    {
        lock (_requests)
        {
            CompatibilityAssert.Require(
                _requests.Count(request =>
                    request.StartsWith(
                        "POST /api/v4/projects/42/merge_requests",
                        StringComparison.Ordinal)) == 2,
                "The fake GitLab boundary did not receive both MR operations.");
            CompatibilityAssert.Require(
                _requests.Count(request =>
                    request.StartsWith(
                        "PUT /api/v4/projects/42/packages/generic/",
                        StringComparison.Ordinal)) == 4,
                "The fake GitLab boundary did not receive both RID archives twice.");
            CompatibilityAssert.Require(
                _requests.Count(request =>
                    request.Equals(
                        "POST /api/v4/projects/42/releases",
                        StringComparison.Ordinal)) == 2,
                "The fake GitLab boundary did not receive both Release operations.");
            CompatibilityAssert.Require(
                _requests.Count(request =>
                    request.Equals(
                        "PUT /api/v2/package/",
                        StringComparison.Ordinal)) is 2 or 4,
                "The fake NuGet boundary did not receive both package operations.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        _stopping.Dispose();
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(
                cancellationToken);
            _ = HandleAsync(client, cancellationToken);
        }
    }

    private async Task HandleAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            var stream = client.GetStream();
            var header = await ReadHeaderAsync(stream, cancellationToken);
            var lines = header.Split(
                "\r\n",
                StringSplitOptions.RemoveEmptyEntries);
            var requestLine = lines[0].Split(' ');
            var method = requestLine[0];
            var target = requestLine[1];
            var path = new Uri(ApiBase, target).AbsolutePath;
            var contentLength = lines
                .Skip(1)
                .Select(line => line.Split(':', 2))
                .Where(parts =>
                    parts.Length == 2
                    && parts[0].Equals(
                        "Content-Length",
                        StringComparison.OrdinalIgnoreCase))
                .Select(parts => int.Parse(
                    parts[1].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture))
                .SingleOrDefault();
            var chunked = lines.Skip(1).Any(line =>
                line.Equals(
                    "Transfer-Encoding: chunked",
                    StringComparison.OrdinalIgnoreCase));

            if (chunked)
            {
                await DrainChunkedAsync(stream, cancellationToken);
            }
            else
            {
                await DrainAsync(stream, contentLength, cancellationToken);
            }

            lock (_requests)
            {
                _requests.Add($"{method} {path}");
            }

            var logPath = Environment.GetEnvironmentVariable(
                "COMPATIBILITY_FAKE_GITLAB_LOG");

            if (logPath is not null)
            {
                await File.AppendAllTextAsync(
                    logPath,
                    $"{method} {path}\n",
                    cancellationToken);
            }

            var (status, body) = CreateResponse(method, path);
            var payload = Encoding.UTF8.GetBytes(body);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\n"
                + "Content-Type: application/json\r\n"
                + $"Content-Length: {payload.Length}\r\n"
                + "Connection: close\r\n\r\n");
            await stream.WriteAsync(response, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
    }

    private (string Status, string Body) CreateResponse(
        string method,
        string path)
    {
        if (method == "GET"
            && path.Equals("/v3/index.json", StringComparison.Ordinal))
        {
            return (
                "200 OK",
                JsonSerializer.Serialize(new
                {
                    version = "3.0.0",
                    resources = new[]
                    {
                        new Dictionary<string, string>()
                        {
                            ["@id"] =
                                $"{ApiBase.GetLeftPart(UriPartial.Authority)}"
                                + "/api/v2/package",
                            ["@type"] = "PackagePublish/2.0.0",
                        },
                    },
                }));
        }

        if (method == "PUT"
            && path.Equals("/api/v2/package/", StringComparison.Ordinal))
        {
            return ("201 Created", "{}");
        }

        if (method == "GET" && path.EndsWith(
                "/merge_requests",
                StringComparison.Ordinal))
        {
            return ("200 OK", "[]");
        }

        if (method == "POST" && path.EndsWith(
                "/merge_requests",
                StringComparison.Ordinal))
        {
            return (
                "201 Created",
                $$"""
                {"iid":7,"web_url":"{{ApiBase.GetLeftPart(UriPartial.Authority)}}/group/project/-/merge_requests/7"}
                """);
        }

        if (method == "GET" && path.Contains(
                "/repository/tags/",
                StringComparison.Ordinal))
        {
            return ("404 Not Found", "{}");
        }

        if (method == "POST" && path.EndsWith(
                "/repository/tags",
                StringComparison.Ordinal))
        {
            return ("201 Created", "{}");
        }

        if (method == "GET" && path.EndsWith(
                "/packages",
                StringComparison.Ordinal))
        {
            return ("200 OK", "[]");
        }

        if (method == "PUT" && path.Contains(
                "/packages/generic/",
                StringComparison.Ordinal))
        {
            return ("201 Created", "{}");
        }

        if (method == "GET" && path.Contains(
                "/releases/",
                StringComparison.Ordinal))
        {
            return ("404 Not Found", "{}");
        }

        if (method == "POST" && path.EndsWith(
                "/releases",
                StringComparison.Ordinal))
        {
            return (
                "201 Created",
                JsonSerializer.Serialize(new
                {
                    _links = new
                    {
                        self =
                            $"{ApiBase.GetLeftPart(UriPartial.Authority)}"
                            + "/group/project/-/releases/"
                            + CompatibilityConstants.FixtureVersion,
                    },
                }));
        }

        return ("500 Internal Server Error", "{}");
    }

    private static async Task<string> ReadHeaderAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();

        while (bytes.Count < 64 * 1024)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            bytes.Add(buffer[0]);

            if (bytes.Count >= 4
                && bytes[^4] == '\r'
                && bytes[^3] == '\n'
                && bytes[^2] == '\r'
                && bytes[^1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }

        throw new InvalidDataException("The fake GitLab request is invalid.");
    }

    private static async Task DrainAsync(
        NetworkStream stream,
        int remaining,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The fake GitLab request body ended early.");
            }

            remaining -= read;
        }
    }

    private static async Task DrainChunkedAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var sizeLine = await ReadLineAsync(stream, cancellationToken);
            var separator = sizeLine.IndexOf(';', StringComparison.Ordinal);
            var sizeText = separator < 0
                ? sizeLine
                : sizeLine[..separator];
            var size = int.Parse(
                sizeText,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture);

            if (size == 0)
            {
                _ = await ReadLineAsync(stream, cancellationToken);
                return;
            }

            await DrainAsync(stream, size, cancellationToken);
            _ = await ReadLineAsync(stream, cancellationToken);
        }
    }

    private static async Task<string> ReadLineAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();

        while (bytes.Count < 8 * 1024)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The fake GitLab chunked body ended early.");
            }

            if (buffer[0] == '\n')
            {
                return Encoding.ASCII.GetString(bytes.ToArray())
                    .TrimEnd('\r');
            }

            bytes.Add(buffer[0]);
        }

        throw new InvalidDataException(
            "The fake GitLab chunk header is too long.");
    }
}
