using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

var root = FindRepositoryRoot();
var version = args.SingleOrDefault()
              ?? throw new InvalidDataException(
                  "Pass the coordinated candidate version.");
ValidateReleaseHistory(root);
ValidateConsumers(root);
ValidateTemplatesAndWorkflow(root);
ValidateCandidatePackages(root, version);

static DirectoryInfo FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "TedToolkit.slnx")))
        {
            return current;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException(
        "Unable to locate the repository root.");
}

static void ValidateReleaseHistory(DirectoryInfo root)
{
    var baselinePath = Path.Combine(
        root.FullName,
        "Build",
        "release-history.v1.json");
    var capturePath = Path.Combine(
        root.FullName,
        "docs",
        "changes",
        "P2-nuget-ready-modular-pipelines",
        "evidence",
        "release-history-capture.v1.json");
    var acceptancePath = Path.Combine(
        root.FullName,
        "docs",
        "changes",
        "P2-nuget-ready-modular-pipelines",
        "evidence",
        "release-history-acceptance.md");
    var hash = Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(baselinePath)));
    using var capture = JsonDocument.Parse(File.ReadAllText(capturePath));
    Require(
        capture.RootElement.GetProperty("schemaVersion").GetInt32() == 1
        && capture.RootElement.GetProperty("completeLocalTagVerification")
            .GetBoolean()
        && capture.RootElement.GetProperty("baselineSha256").GetString()
            == hash
        && File.ReadAllText(acceptancePath).Contains(
            hash,
            StringComparison.Ordinal),
        "Release-history acceptance or hash is invalid.");
}

static void ValidateConsumers(DirectoryInfo root)
{
    var consumers = Path.Combine(root.FullName, "tests", "Consumers");
    var projects = Directory.GetFiles(
            consumers,
            "*.csproj",
            SearchOption.AllDirectories)
        .Where(project =>
            Path.GetRelativePath(consumers, project)
                .Split(Path.DirectorySeparatorChar).Length == 2)
        .ToArray();
    Require(
        projects.Length == 4,
        "Exactly four package-only consumer hosts are required.");

    foreach (var project in projects)
    {
        var document = XDocument.Load(project);
        var references = document.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .ToArray();
        Require(
            !document.Descendants("ProjectReference").Any(),
            $"{Path.GetFileName(project)} must be package-only.");
        Require(
            references.Contains(
                "TedToolkit.ModularPipelines.Build",
                StringComparer.Ordinal),
            $"{Path.GetFileName(project)} must directly reference Build.");

        if (project.Contains(
                $"{Path.DirectorySeparatorChar}BuildOnly{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            Require(
                !references.Contains(
                    "TedToolkit.ModularPipelines.Combine",
                    StringComparer.Ordinal),
                "BuildOnly must not resolve Combine.");
        }
        else
        {
            Require(
                references.Contains(
                    "TedToolkit.ModularPipelines.Combine",
                    StringComparer.Ordinal),
                "Provider consumers must directly reference Combine.");
        }
    }

    var baseline = File.ReadAllText(Path.Combine(
        consumers,
        "consumer-baselines.json"));
    Require(
        baseline.Contains(
            "EverythingButTheSink",
            StringComparison.Ordinal)
        && baseline.Contains(
            "6ccbdc3498be6ef3c3d14a91646bb6649c46f5c9",
            StringComparison.Ordinal)
        && !baseline.Contains("http", StringComparison.OrdinalIgnoreCase)
        && !baseline.Contains(":\\", StringComparison.Ordinal),
        "The derived-consumer provenance allowlist is invalid.");
    var compatibility = File.ReadAllText(Path.Combine(
        consumers,
        "EverythingButTheSink",
        "Program.cs"));
    Require(
        compatibility.Contains(
            "PipelineInputMode.ConsumeArtifacts",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "PipelineProfile.Message",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "PipelineProfile.Publish",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "IPipelineEventSink",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "IChangeDescriptionGenerator",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "linux-x64",
            StringComparison.Ordinal)
        && compatibility.Contains(
            "win-x64",
            StringComparison.Ordinal),
        "The derived package-only compatibility fixture is incomplete.");
}

static void ValidateTemplatesAndWorkflow(DirectoryInfo root)
{
    var templates = Path.Combine(
        root.FullName,
        "tests",
        "Consumers",
        "Templates");
    var github = File.ReadAllText(Path.Combine(
        templates,
        "github-actions.yml"));
    var gitlab = File.ReadAllText(Path.Combine(
        templates,
        "gitlab-ci.yml"));
    Require(
        github.Contains("fetch-depth: 0", StringComparison.Ordinal)
        && github.Contains(
            "tests/Consumers/GitHub/GitHub.csproj",
            StringComparison.Ordinal)
        && github.Contains(
            "dotnet run --project tests/Consumers/GitHub/GitHub.csproj",
            StringComparison.Ordinal)
        && !github.Contains("secrets.", StringComparison.Ordinal)
        && gitlab.Contains("GIT_DEPTH: \"0\"", StringComparison.Ordinal)
        && gitlab.Contains("artifacts: true", StringComparison.Ordinal)
        && gitlab.Contains(
            "CONSUME_ARTIFACTS: \"true\"",
            StringComparison.Ordinal)
        && gitlab.LastIndexOf(
            "dotnet restore tests/Consumers/GitLab/GitLab.csproj",
            StringComparison.Ordinal)
        > gitlab.IndexOf("combine:", StringComparison.Ordinal)
        && !gitlab.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
        && !gitlab.Contains("https://", StringComparison.OrdinalIgnoreCase),
        "A side-effect-free CI template is invalid.");

    var workflow = File.ReadAllText(Path.Combine(
        root.FullName,
        ".github",
        "workflows",
        "build.yml"));
    Require(
        workflow.Contains("permissions:\n  contents: read", StringComparison.Ordinal)
        && workflow.Contains("queue: max", StringComparison.Ordinal)
        && workflow.Contains(
            "cancel-in-progress: false",
            StringComparison.Ordinal)
        && workflow.Contains("fetch-depth: 0", StringComparison.Ordinal)
        && workflow.Contains(
            "consumer-package-dry-run:",
            StringComparison.Ordinal)
        && workflow.Contains(
            "Restore package-only consumers",
            StringComparison.Ordinal)
        && workflow.Contains(
            "EverythingButTheSink.csproj",
            StringComparison.Ordinal)
        && workflow.Contains(
            "-- consume",
            StringComparison.Ordinal)
        && !workflow.Contains(
            "pull-requests: write",
            StringComparison.Ordinal)
        && workflow.IndexOf(
            "NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}",
            StringComparison.Ordinal)
        > workflow.IndexOf("publish:", StringComparison.Ordinal),
        "The repository workflow violates least-privilege release policy.");
}

static void ValidateCandidatePackages(
    DirectoryInfo root,
    string version)
{
    var source = Path.Combine(
        root.FullName,
        "artifacts",
        "candidate-packages");
    var expected = new[]
    {
        "TedToolkit.CodeAnalysis",
        "TedToolkit.ModularPipelines.Build",
        "TedToolkit.ModularPipelines.Combine",
    };
    var nuspecs = new Dictionary<string, XDocument>(
        StringComparer.Ordinal);

    foreach (var packageId in expected)
    {
        var packagePath = Path.Combine(
            source,
            $"{packageId}.{version}.nupkg");
        Require(File.Exists(packagePath), $"Missing {packageId} candidate.");
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspecEntry = archive.Entries.Single(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspecEntry.Open();
        var nuspec = XDocument.Load(stream);
        var metadata = nuspec.Descendants()
            .Single(element => element.Name.LocalName == "metadata");
        var iconPath = metadata.Elements()
            .Single(element => element.Name.LocalName == "icon").Value;
        Require(
            metadata.Elements().Single(element =>
                    element.Name.LocalName == "id").Value
                == packageId
            && metadata.Elements().Single(element =>
                    element.Name.LocalName == "version").Value
                == version
            && archive.Entries.Any(entry =>
                entry.FullName.Equals(
                    "README.md",
                    StringComparison.OrdinalIgnoreCase))
            && archive.Entries.Any(entry =>
                entry.FullName.Equals(
                    iconPath,
                    StringComparison.OrdinalIgnoreCase)),
            $"{packageId} metadata/assets are invalid.");
        nuspecs.Add(packageId, nuspec);

        if (packageId.EndsWith(
                ".Build",
                StringComparison.Ordinal)
            || packageId.EndsWith(
                ".Combine",
                StringComparison.Ordinal))
        {
            ValidateCompiledPackage(archive, packageId, version);
            Require(
                File.Exists(Path.Combine(
                    source,
                    $"{packageId}.{version}.snupkg")),
                $"{packageId} symbol package is missing.");
        }
        else
        {
            Require(
                archive.Entries.Any(entry =>
                    entry.FullName.StartsWith(
                        "analyzers/dotnet/cs/",
                        StringComparison.Ordinal)),
                "CodeAnalysis analyzer assets are missing.");
        }
    }

    var combineDependencies = nuspecs[
            "TedToolkit.ModularPipelines.Combine"]
        .Descendants()
        .Where(element => element.Name.LocalName == "dependency")
        .Where(element =>
            (string?)element.Attribute("id")
            == "TedToolkit.ModularPipelines.Build")
        .ToArray();
    Require(
        combineDependencies.Length == 1
        && (string?)combineDependencies[0].Attribute("version")
        == $"[{version}]",
        "Combine must depend on the exact same Build version.");

    var manifestPath = Path.Combine(
        root.FullName,
        "output",
        "pipeline-artifacts.v1.json");
    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
    Require(
        manifest.RootElement.GetProperty("packageVersion").GetString()
        == version,
        "Manifest and package versions differ.");
}

static void ValidateCompiledPackage(
    ZipArchive archive,
    string packageId,
    string version)
{
    var dll = archive.Entries.Single(entry =>
        entry.FullName.Equals(
            $"lib/net10.0/{packageId}.dll",
            StringComparison.Ordinal));
    var temporaryPath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid():N}-{packageId}.dll");

    try
    {
        dll.ExtractToFile(temporaryPath);
        var expectedAssemblyVersion = Version.Parse(
            version.Count(character => character == '.') == 2
                ? $"{version}.0"
                : version);
        var assemblyVersion = AssemblyName.GetAssemblyName(
            temporaryPath).Version;
        var fileVersion = FileVersionInfo.GetVersionInfo(
            temporaryPath).FileVersion;
        var informationalVersion = FileVersionInfo.GetVersionInfo(
            temporaryPath).ProductVersion;
        Require(
            assemblyVersion == expectedAssemblyVersion
            && fileVersion == expectedAssemblyVersion.ToString()
            && informationalVersion is not null
            && informationalVersion.StartsWith(
                version,
                StringComparison.Ordinal),
            $"{packageId} compiled version metadata is invalid.");
    }
    finally
    {
        File.Delete(temporaryPath);
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidDataException(message);
    }
}
