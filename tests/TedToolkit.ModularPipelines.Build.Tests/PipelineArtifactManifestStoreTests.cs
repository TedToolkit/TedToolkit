using System.Diagnostics;
using System.Security.Cryptography;
using System.IO.Compression;

using TedToolkit.ModularPipelines.Build.Artifacts;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class PipelineArtifactManifestStoreTests
{
    /// <summary>
    /// 验证清单以确定性 schema 1 JSON 往返并校验源修订与文件哈希。
    /// </summary>
    [Test]
    public async Task Should_round_trip_and_validate_a_manifest()
    {
        using var root = TemporaryDirectory.Create();
        var artifactPath = Path.Combine(root.Directory.FullName, "test", "sample.trx");
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        await File.WriteAllTextAsync(artifactPath, "<TestRun />");
        var file = new FileInfo(artifactPath);
        var artifact = new PipelineArtifact
        {
            RelativePath = "test/sample.trx",
            Kind = PipelineArtifactKind.TestResult,
            LogicalName = "sample",
            Sha256 = await HashAsync(file),
            Size = file.Length,
        };
        var manifest = new PipelineArtifactManifest
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = "abc123",
            SourceTreeDirty = false,
            Artifacts = [artifact],
        };
        var store = new PipelineArtifactManifestStore();

        var manifestFile = await store.WriteAsync(
            root.Directory,
            "pipeline-artifacts.v1.json",
            manifest);
        var restored = await store.ReadAsync(
            root.Directory,
            manifestFile,
            new ArtifactValidationOptions
            {
                ExpectedSourceRevision = "abc123",
            });

        await Assert.That(restored).IsEquivalentTo(manifest);

        await Assert.That(async () => await store.ReadAsync(
                root.Directory,
                manifestFile,
                new ArtifactValidationOptions
                {
                    ExpectedSourceRevision = "different",
                }))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证篡改、未列入清单的残留文件和路径逃逸均会被拒绝。
    /// </summary>
    [Test]
    public async Task Should_reject_tampered_unlisted_or_escaping_artifacts()
    {
        using var root = TemporaryDirectory.Create();
        var artifactPath = Path.Combine(root.Directory.FullName, "sample.trx");
        await File.WriteAllTextAsync(artifactPath, "original");
        var file = new FileInfo(artifactPath);
        var store = new PipelineArtifactManifestStore();
        var manifest = new PipelineArtifactManifest
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = "abc123",
            SourceTreeDirty = false,
            Artifacts =
            [
                new PipelineArtifact
                {
                    RelativePath = "sample.trx",
                    Kind = PipelineArtifactKind.TestResult,
                    LogicalName = "sample",
                    Sha256 = await HashAsync(file),
                    Size = file.Length,
                },
            ],
        };
        var manifestFile = await store.WriteAsync(
            root.Directory,
            "pipeline-artifacts.v1.json",
            manifest);

        await File.WriteAllTextAsync(artifactPath, "tampered");

        await Assert.That(async () => await store.ReadAsync(
                root.Directory,
                manifestFile))
            .Throws<InvalidDataException>();

        await File.WriteAllTextAsync(artifactPath, "original");
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "unlisted.txt"),
            "residual");

        await Assert.That(async () => await store.ReadAsync(
                root.Directory,
                manifestFile))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证读取器从清单父目录推导制品根，而不会把消费者根中的无关文件视为残留制品。
    /// </summary>
    [Test]
    public async Task Should_derive_the_artifact_root_from_the_manifest_parent()
    {
        using var consumer = TemporaryDirectory.Create();
        var artifactRoot = consumer.Directory.CreateSubdirectory("handoff");
        await File.WriteAllTextAsync(
            Path.Combine(consumer.Directory.FullName, "consumer.txt"),
            "outside-artifact-root");
        var artifactPath = Path.Combine(
            artifactRoot.FullName,
            "test",
            "sample.trx");
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        await File.WriteAllTextAsync(artifactPath, "<TestRun />");
        var artifactFile = new FileInfo(artifactPath);
        var manifest = new PipelineArtifactManifest
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = "abc123",
            SourceTreeDirty = false,
            Artifacts =
            [
                new PipelineArtifact
                {
                    RelativePath = "test/sample.trx",
                    Kind = PipelineArtifactKind.TestResult,
                    LogicalName = "sample",
                    Sha256 = await HashAsync(artifactFile),
                    Size = artifactFile.Length,
                },
            ],
        };
        var store = new PipelineArtifactManifestStore();
        var manifestFile = await store.WriteAsync(
            artifactRoot,
            "pipeline-artifacts.v1.json",
            manifest);

        var restored = await store.ReadAsync(
            consumer.Directory,
            manifestFile,
            new ArtifactValidationOptions
            {
                ExpectedSourceRevision = "abc123",
            });

        await Assert.That(restored.Artifacts).Count().IsEqualTo(1);
    }

    /// <summary>
    /// 验证未知或重复 JSON 属性、非 schema 1 以及 Running 持久状态均被拒绝。
    /// </summary>
    [Test]
    public async Task Should_reject_noncanonical_manifest_json()
    {
        using var root = TemporaryDirectory.Create();
        var manifestPath = Path.Combine(
            root.Directory.FullName,
            "pipeline-artifacts.v1.json");
        var invalidDocuments = new[]
        {
            """
            {"schemaVersion":2,"status":"Succeeded","sourceRevision":"abc","sourceTreeDirty":false,"createdAtUtc":"2026-07-29T00:00:00+00:00","packageVersion":null,"targets":[],"tests":[],"artifacts":[],"failures":[]}
            """,
            """
            {"schemaVersion":1,"status":"Running","sourceRevision":"abc","sourceTreeDirty":false,"createdAtUtc":"2026-07-29T00:00:00+00:00","packageVersion":null,"targets":[],"tests":[],"artifacts":[],"failures":[]}
            """,
            """
            {"schemaVersion":1,"schemaVersion":1,"status":"Succeeded","sourceRevision":"abc","sourceTreeDirty":false,"createdAtUtc":"2026-07-29T00:00:00+00:00","packageVersion":null,"targets":[],"tests":[],"artifacts":[],"failures":[]}
            """,
            """
            {"schemaVersion":1,"status":"Succeeded","sourceRevision":"abc","sourceTreeDirty":false,"createdAtUtc":"2026-07-29T00:00:00+00:00","packageVersion":null,"targets":[],"tests":[],"artifacts":[],"failures":[],"unknown":true}
            """,
        };
        var store = new PipelineArtifactManifestStore();

        foreach (var document in invalidDocuments)
        {
            await File.WriteAllTextAsync(manifestPath, document);

            await Assert.That(async () => await store.ReadAsync(
                    root.Directory,
                    new FileInfo(manifestPath),
                    new ArtifactValidationOptions
                    {
                        ExpectedSourceRevision = "abc",
                    }))
                .ThrowsException();
        }
    }

    /// <summary>
    /// 验证主包和符号包按规范化 nuspec 身份关联，并拒绝重复主包身份。
    /// </summary>
    [Test]
    public async Task Should_validate_normalized_package_identity_relations()
    {
        using var root = TemporaryDirectory.Create();
        var nugetDirectory = root.Directory.CreateSubdirectory("nuget");
        var primary = await CreatePackageAsync(
            nugetDirectory,
            "first.nupkg",
            "Sample.Package",
            "1.2.3.0");
        var symbol = await CreatePackageAsync(
            nugetDirectory,
            "first.snupkg",
            "Sample.Package",
            "1.2.3");
        var artifacts = new[]
        {
            await CreatePackageArtifactAsync(
                primary,
                PipelineArtifactKind.NuGetPackage,
                "1.2.3"),
            await CreatePackageArtifactAsync(
                symbol,
                PipelineArtifactKind.SymbolPackage,
                "1.2.3"),
        };
        var manifest = new PipelineArtifactManifest
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = "abc123",
            SourceTreeDirty = false,
            PackageVersion = "1.2.3",
            Artifacts = artifacts,
        };
        var store = new PipelineArtifactManifestStore();

        var manifestFile = await store.WriteAsync(
            root.Directory,
            "pipeline-artifacts.v1.json",
            manifest);
        var restored = await store.ReadAsync(
            root.Directory,
            manifestFile,
            new ArtifactValidationOptions
            {
                ExpectedSourceRevision = "abc123",
            });

        await Assert.That(restored.PackageVersion).IsEqualTo("1.2.3");
        File.Delete(manifestFile.FullName);

        var duplicate = await CreatePackageAsync(
            nugetDirectory,
            "second.nupkg",
            "Sample.Package",
            "1.2.3");
        var duplicateArtifact = await CreatePackageArtifactAsync(
            duplicate,
            PipelineArtifactKind.NuGetPackage,
            "1.2.3");
        await Assert.That(async () => await store.WriteAsync(
                root.Directory,
                "duplicate.json",
                manifest with
                {
                    Artifacts = [.. artifacts, duplicateArtifact,],
                }))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证含路径遍历项的发布归档不能进入结构有效的清单。
    /// </summary>
    [Test]
    public async Task Should_reject_unsafe_publish_archive_entries()
    {
        using var root = TemporaryDirectory.Create();
        var publishDirectory = root.Directory.CreateSubdirectory("publish");
        var archivePath = Path.Combine(
            publishDirectory.FullName,
            "unsafe.zip");
        using (var archive = ZipFile.Open(
                   archivePath,
                   ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escape.txt");
            await using var stream = entry.Open();
            await stream.WriteAsync("unsafe"u8.ToArray());
        }

        var file = new FileInfo(archivePath);
        var manifest = new PipelineArtifactManifest
        {
            Status = PipelineRunStatus.Succeeded,
            SourceRevision = "abc123",
            SourceTreeDirty = false,
            Artifacts =
            [
                new PipelineArtifact
                {
                    RelativePath = "publish/unsafe.zip",
                    Kind = PipelineArtifactKind.PublishArchive,
                    LogicalName = "unsafe",
                    Sha256 = await HashAsync(file),
                    Size = file.Length,
                },
            ],
        };

        await Assert.That(async () =>
                await new PipelineArtifactManifestStore().WriteAsync(
                    root.Directory,
                    "pipeline-artifacts.v1.json",
                    manifest))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证未显式提供预期修订时，读取器使用可用的当前 Git HEAD 执行默认匹配。
    /// </summary>
    [Test]
    public async Task Should_use_current_git_revision_when_expected_revision_is_absent()
    {
        using var root = TemporaryDirectory.Create();
        await RunGitAsync(root.Directory, "init");
        await RunGitAsync(
            root.Directory,
            "config",
            "user.email",
            "tests@example.invalid");
        await RunGitAsync(
            root.Directory,
            "config",
            "user.name",
            "TedToolkit Tests");
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "tracked.txt"),
            "tracked");
        await RunGitAsync(root.Directory, "add", "tracked.txt");
        await RunGitAsync(root.Directory, "commit", "-m", "initial");
        var revision = await RunGitAsync(
            root.Directory,
            "rev-parse",
            "HEAD");
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var store = new PipelineArtifactManifestStore();
        var manifestFile = await store.WriteAsync(
            artifactRoot,
            "pipeline-artifacts.v1.json",
            new PipelineArtifactManifest
            {
                Status = PipelineRunStatus.Succeeded,
                SourceRevision = revision,
                SourceTreeDirty = false,
            });

        var restored = await store.ReadAsync(
            root.Directory,
            manifestFile);

        await Assert.That(restored.SourceRevision).IsEqualTo(revision);

        await File.WriteAllTextAsync(
            manifestFile.FullName,
            (await File.ReadAllTextAsync(manifestFile.FullName))
            .Replace(
                revision,
                new string('0', revision.Length),
                StringComparison.Ordinal));
        await Assert.That(async () => await store.ReadAsync(
                root.Directory,
                manifestFile))
            .Throws<InvalidDataException>();
    }

    private static async Task<string> HashAsync(FileInfo file)
    {
        await using var stream = file.OpenRead();
        return Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream));
    }

    private static async Task<FileInfo> CreatePackageAsync(
        DirectoryInfo directory,
        string fileName,
        string packageId,
        string version)
    {
        var path = Path.Combine(directory.FullName, fileName);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry($"{packageId}.nuspec");
            await using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(
                $"<package><metadata><id>{packageId}</id><version>{version}</version></metadata></package>");
        }

        return new FileInfo(path);
    }

    private static async Task<PipelineArtifact> CreatePackageArtifactAsync(
        FileInfo file,
        PipelineArtifactKind kind,
        string normalizedVersion)
    {
        return new PipelineArtifact
        {
            RelativePath = $"nuget/{file.Name}",
            Kind = kind,
            LogicalName = "Sample.Package",
            Sha256 = await HashAsync(file),
            Size = file.Length,
            PackageId = "Sample.Package",
            PackageVersion = normalizedVersion,
        };
    }

    private static async Task<string> RunGitAsync(
        DirectoryInfo workingDirectory,
        params string[] arguments)
    {
        using var process = new Process()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error);
        }

        return output.Trim();
    }
}