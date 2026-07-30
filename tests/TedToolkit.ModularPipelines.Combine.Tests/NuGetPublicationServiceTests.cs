using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Internal;

namespace TedToolkit.ModularPipelines.Combine.Tests;

internal sealed class NuGetPublicationServiceTests
{
    /// <summary>
    /// 验证检查点先读取、逐包记录、最后完成，且秘密仅在存在剩余命令时解析。
    /// </summary>
    [Test]
    public async Task Should_checkpoint_each_confirmed_push_in_order()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = Directory.CreateDirectory(Path.Combine(
            root.Directory.FullName,
            "artifacts"));
        var first = CreatePackage(artifactRoot, "A.Package", "1.2.3");
        var second = CreatePackage(artifactRoot, "B.Package", "1.2.3");
        var manifest = CreateManifest(first, second);
        var store = new PipelineArtifactManifestStore();
        var manifestFile = await store.WriteAsync(
            artifactRoot,
            "pipeline-artifacts.v1.json",
            manifest);
        var calls = new List<string>();
        var checkpoint = new RecordingCheckpoint(calls);
        var executor = new RecordingExecutor(calls);
        var resolver = new RecordingSecretResolver(calls);
        var service = new NuGetPublicationService(
            executor,
            resolver);
        var registration = CreateRegistration(
            root.Directory,
            manifestFile,
            checkpoint);

        var result = await service.PublishAsync(
            registration,
            manifest,
            artifactRoot,
            CancellationToken.None);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(executor.Requests.All(request =>
            !request.Arguments.Contains(
                "--non-interactive",
                StringComparer.Ordinal))).IsTrue();
        await Assert.That(executor.Requests.All(request =>
            !request.Arguments.Contains(
                "--allow-insecure-connections",
                StringComparer.Ordinal))).IsTrue();
        await Assert.That(calls).IsEquivalentTo(new[]
        {
            "checkpoint:read",
            "secret:NUGET_KEY",
            "push:A.Package.1.2.3.nupkg",
            "checkpoint:record:A.Package",
            "push:B.Package.1.2.3.nupkg",
            "checkpoint:record:B.Package",
            "checkpoint:complete",
        });
    }

    /// <summary>
    /// 验证全部包已有可信完成证据时不会解析秘密或执行命令。
    /// </summary>
    [Test]
    public async Task Should_make_an_all_complete_checkpoint_zero_command()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = Directory.CreateDirectory(Path.Combine(
            root.Directory.FullName,
            "artifacts"));
        var package = CreatePackage(
            artifactRoot,
            "A.Package",
            "1.2.3");
        var manifest = CreateManifest(package);
        var store = new PipelineArtifactManifestStore();
        var manifestFile = await store.WriteAsync(
            artifactRoot,
            "pipeline-artifacts.v1.json",
            manifest);
        var calls = new List<string>();
        var checkpoint = new RecordingCheckpoint(
            calls,
            ["A.Package"]);
        var service = new NuGetPublicationService(
            new RecordingExecutor(calls),
            new RecordingSecretResolver(calls));

        var result = await service.PublishAsync(
            CreateRegistration(
                root.Directory,
                manifestFile,
                checkpoint),
            manifest,
            artifactRoot,
            CancellationToken.None);

        await Assert.That(result).IsEmpty();
        await Assert.That(calls).IsEquivalentTo(new[]
        {
            "checkpoint:read",
            "checkpoint:complete",
        });
    }

    private static ValidatedCombineRegistration CreateRegistration(
        DirectoryInfo root,
        FileInfo manifest,
        IPackagePublicationCheckpoint? checkpoint)
    {
        var options = new CombineOptions
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = PipelineProfile.Publish,
            InputMode = PipelineInputMode.ConsumeArtifacts,
            ArtifactManifestPath = Path.GetRelativePath(
                root.FullName,
                manifest.FullName),
            Actions = new()
            {
                PushNuGetPackages = true,
            },
            NuGetPush = new()
            {
                Source = new("http://nuget.example/v3/index.json"),
                CredentialReference = "NUGET_KEY",
                AllowInsecureHttp = true,
                UsePackagePublicationCheckpoint = checkpoint is not null,
            },
            PackagePublicationCheckpoint = checkpoint,
        };
        return CombineOptionsValidator.Validate(
            root,
            options,
            null,
            new BuildOptions(),
            null);
    }

    private static PipelineArtifactManifest CreateManifest(
        params PipelineArtifact[] packages)
    {
        return new()
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = new string('a', 40),
            SourceTreeDirty = false,
            PackageVersion = "1.2.3",
            Artifacts = packages,
        };
    }

    private static PipelineArtifact CreatePackage(
        DirectoryInfo root,
        string packageId,
        string version)
    {
        var fileName = $"{packageId}.{version}.nupkg";
        var path = Path.Combine(root.FullName, fileName);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry($"{packageId}.nuspec");
            using var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false));
            writer.Write(
                $"<package><metadata><id>{packageId}</id><version>{version}</version></metadata></package>");
        }

        var bytes = File.ReadAllBytes(path);
        return new()
        {
            RelativePath = fileName,
            Kind = PipelineArtifactKind.NuGetPackage,
            LogicalName = packageId,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Size = bytes.Length,
            PackageId = packageId,
            PackageVersion = version,
        };
    }

    private sealed class RecordingExecutor(List<string> calls)
        : ICombineCommandExecutor
    {
        public List<CombineCommandRequest> Requests { get; } = [];

        public Task<CombineCommandResult> ExecuteAsync(
            CombineCommandRequest request,
            string? sensitiveArgument,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            calls.Add($"push:{Path.GetFileName(request.Arguments[2])}");
            return Task.FromResult(new CombineCommandResult(
                0,
                false,
                false,
                false));
        }
    }

    private sealed class RecordingSecretResolver(List<string> calls)
        : IPipelineSecretResolver
    {
        public ValueTask<string?> ResolveAsync(
            string credentialReference,
            CancellationToken cancellationToken)
        {
            calls.Add($"secret:{credentialReference}");
            return ValueTask.FromResult<string?>("resolved-secret");
        }
    }

    private sealed class RecordingCheckpoint(
        List<string> calls,
        IReadOnlyList<string>? completed = null)
        : IPackagePublicationCheckpoint
    {
        public Task<IReadOnlyList<string>> ReadCompletedPackageIdsAsync(
            PackagePublicationCheckpointContext context,
            CancellationToken cancellationToken)
        {
            calls.Add("checkpoint:read");
            return Task.FromResult(completed ?? []);
        }

        public Task RecordCompletedAsync(
            PackagePublicationCheckpointContext context,
            PackagePublicationCheckpointRecord package,
            CancellationToken cancellationToken)
        {
            calls.Add($"checkpoint:record:{package.PackageId}");
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            PackagePublicationCheckpointContext context,
            CancellationToken cancellationToken)
        {
            calls.Add("checkpoint:complete");
            return Task.CompletedTask;
        }
    }
}
