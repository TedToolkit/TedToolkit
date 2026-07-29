using System.Diagnostics;

using TedToolkit.Build.ReleaseLifecycle;
using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.HostCompatibility.Tests;

internal sealed class ReleaseLifecycleTests
{
    private static readonly string ManifestHash = new('a', 64);
    private static readonly PackagePublicationCheckpointRecord[] Packages =
    [
        new("TedToolkit.CodeAnalysis", new string('1', 64), new string('2', 64)),
        new("TedToolkit.ModularPipelines.Build", new string('3', 64), new string('4', 64)),
        new("TedToolkit.ModularPipelines.Combine", new string('5', 64), new string('6', 64)),
    ];

    [Test]
    public async Task Should_resolve_checkpoint_and_retry_terminal_publication()
    {
        using var repository = await TestRepository.CreateAsync();
        var baseline = new FileInfo(Path.Combine(
            repository.WorkingDirectory.FullName,
            "release-history.v1.json"));
        await File.WriteAllTextAsync(
            baseline.FullName,
            "{\"schemaVersion\":1,\"releases\":[]}");
        var resolver = new RepositoryReleaseVersionResolver(
            repository.WorkingDirectory);
        var version = await resolver.ResolveAsync(
            new DateOnly(2026, 7, 29),
            repository.Revision,
            baseline,
            CancellationToken.None);
        await Assert.That(version).IsEqualTo("2026.7.29");

        var checkpoint = new GitTagPackagePublicationCheckpoint(
            repository.WorkingDirectory,
            "github:101");
        var context = new PackagePublicationCheckpointContext(
            version,
            repository.Revision,
            ManifestHash,
            Packages);
        await Assert.That(
                await checkpoint.ReadCompletedPackageIdsAsync(
                    context,
                    CancellationToken.None))
            .IsEmpty();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(
                new DateOnly(2026, 7, 29),
                repository.Revision,
                baseline,
                CancellationToken.None));

        foreach (var package in Packages)
        {
            await checkpoint.RecordCompletedAsync(
                context,
                package,
                CancellationToken.None);
        }

        await checkpoint.CompleteAsync(context, CancellationToken.None);
        await checkpoint.CleanupPendingAsync(CancellationToken.None);
        var remoteTagsAfterSuccess = await repository.ListRemoteTagsAsync();

        var retry = new GitTagPackagePublicationCheckpoint(
            repository.WorkingDirectory,
            "github:101");
        await Assert.That(
                await retry.PrepareAsync(
                    version,
                    CancellationToken.None))
            .IsTrue();
        await Assert.That(await repository.ListRemoteTagsAsync())
            .IsEquivalentTo(remoteTagsAfterSuccess);
        await Assert.That(
                await resolver.ResolveAsync(
                    new DateOnly(2026, 7, 29),
                    repository.Revision,
                    baseline,
                    CancellationToken.None))
            .IsEqualTo("2026.7.29.1");
    }

    [Test]
    public async Task Should_abandon_only_matching_pending_evidence()
    {
        using var repository = await TestRepository.CreateAsync();
        var version = "2026.7.29";
        var checkpoint = new GitTagPackagePublicationCheckpoint(
            repository.WorkingDirectory,
            "github:201");
        var context = new PackagePublicationCheckpointContext(
            version,
            repository.Revision,
            ManifestHash,
            Packages);
        _ = await checkpoint.ReadCompletedPackageIdsAsync(
            context,
            CancellationToken.None);

        var result = await new ReleaseRecoveryCoordinator(
                repository.WorkingDirectory)
            .AbandonAsync(
                version,
                "incident-42",
                CancellationToken.None);
        await Assert.That(result).IsEqualTo("abandoned");
        var tags = await repository.ListRemoteTagsAsync();
        await Assert.That(tags).Contains("nuget-abandoned/2026.7.29");
        await Assert.That(tags).DoesNotContain("nuget-pending/2026.7.29");

        var retry = new GitTagPackagePublicationCheckpoint(
            repository.WorkingDirectory,
            "github:999");
        await Assert.That(
                await retry.PrepareAsync(
                    version,
                    CancellationToken.None))
            .IsTrue();
        await Assert.That(await repository.ListRemoteTagsAsync())
            .IsEquivalentTo(tags);
    }

    [Test]
    public async Task Should_finalize_release_once_before_pending_cleanup()
    {
        using var repository = await TestRepository.CreateAsync();
        var version = "2026.7.29";
        var checkpoint = new GitTagPackagePublicationCheckpoint(
            repository.WorkingDirectory,
            "github:301");
        var context = new PackagePublicationCheckpointContext(
            version,
            repository.Revision,
            ManifestHash,
            Packages);
        _ = await checkpoint.ReadCompletedPackageIdsAsync(
            context,
            CancellationToken.None);

        foreach (var package in Packages)
        {
            await checkpoint.RecordCompletedAsync(
                context,
                package,
                CancellationToken.None);
        }

        await checkpoint.CompleteAsync(context, CancellationToken.None);
        var finalizerCalls = 0;
        var coordinator = new ReleaseRecoveryCoordinator(
            repository.WorkingDirectory);
        var result = await coordinator.FinalizeAsync(
            version,
            "incident-84",
            "github:302",
            (revision, _) =>
            {
                finalizerCalls++;
                return revision.Equals(
                    repository.Revision,
                    StringComparison.OrdinalIgnoreCase)
                    ? Task.CompletedTask
                    : throw new InvalidOperationException(
                        "The recovery target changed.");
            },
            CancellationToken.None);
        await Assert.That(result).IsEqualTo("finalized");
        await Assert.That(finalizerCalls).IsEqualTo(1);
        var tags = await repository.ListRemoteTagsAsync();
        await Assert.That(tags).Contains("nuget-finalized/2026.7.29");
        await Assert.That(tags).DoesNotContain("nuget-pending/2026.7.29");

        var retryResult = await coordinator.FinalizeAsync(
            version,
            "incident-84",
            "github:999",
            (_, _) =>
            {
                finalizerCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);
        await Assert.That(retryResult).IsEqualTo("already-finalized");
        await Assert.That(finalizerCalls).IsEqualTo(1);
        await Assert.That(await repository.ListRemoteTagsAsync())
            .IsEquivalentTo(tags);
    }

    private sealed class TestRepository : IDisposable
    {
        private TestRepository(
            DirectoryInfo root,
            DirectoryInfo workingDirectory,
            DirectoryInfo origin,
            string revision)
        {
            Root = root;
            WorkingDirectory = workingDirectory;
            Origin = origin;
            Revision = revision;
        }

        public DirectoryInfo Root { get; }

        public DirectoryInfo WorkingDirectory { get; }

        public DirectoryInfo Origin { get; }

        public string Revision { get; }

        public static async Task<TestRepository> CreateAsync()
        {
            var root = Directory.CreateDirectory(Path.Combine(
                Path.GetTempPath(),
                $"TedToolkit-ReleaseLifecycle-{Guid.NewGuid():N}"));
            var origin = Directory.CreateDirectory(Path.Combine(
                root.FullName,
                "origin.git"));
            var working = Directory.CreateDirectory(Path.Combine(
                root.FullName,
                "working"));
            await RunAsync(root, "init", "--bare", origin.FullName);
            await RunAsync(working, "init");
            await RunAsync(working, "config", "user.name", "TedToolkit Test");
            await RunAsync(working, "config", "user.email", "test@example.invalid");
            await File.WriteAllTextAsync(
                Path.Combine(working.FullName, "README.md"),
                "test");
            await RunAsync(working, "add", "README.md");
            await RunAsync(working, "commit", "-m", "test");
            await RunAsync(working, "remote", "add", "origin", origin.FullName);
            await RunAsync(working, "push", "-u", "origin", "HEAD:main");
            var revision = (await RunAsync(
                working,
                "rev-parse",
                "HEAD")).Trim();
            return new(root, working, origin, revision);
        }

        public async Task<string[]> ListRemoteTagsAsync()
        {
            return (await RunAsync(
                    WorkingDirectory,
                    "ls-remote",
                    "--tags",
                    "--refs",
                    Origin.FullName))
                .Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line[(line.IndexOf("refs/tags/", StringComparison.Ordinal)
                    + "refs/tags/".Length)..])
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        public void Dispose()
        {
            foreach (var entry in Root.EnumerateFileSystemInfos(
                         "*",
                         SearchOption.AllDirectories))
            {
                entry.Attributes = FileAttributes.Normal;
            }

            Root.Delete(recursive: true);
        }

        private static async Task<string> RunAsync(
            DirectoryInfo workingDirectory,
            params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new()
                {
                    FileName = "git",
                    WorkingDirectory = workingDirectory.FullName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
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
                throw new InvalidOperationException(
                    $"git {string.Join(' ', arguments)} failed: {error}");
            }

            return output;
        }
    }
}
