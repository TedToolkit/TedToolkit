using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Events;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Tests;

internal sealed class EventDispatchTests
{
    /// <summary>
    /// 验证事件快照携带有序的 200 条上限提交，并冻结终态时间。
    /// </summary>
    [Test]
    public async Task Should_dispatch_bounded_immutable_event_snapshots()
    {
        using var root = TemporaryDirectory.Create();
        var versionFile = new FileInfo(Path.Combine(
            root.Directory.FullName,
            "fixture.props"));
        await File.WriteAllTextAsync(
            versionFile.FullName,
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var sink = new RecordingSink();
        var options = new CombineOptions
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = PipelineProfile.Validate,
            ExecutionContext = new()
            {
                Trigger =
                    TedToolkit.ModularPipelines.Build.Conventions
                        .PipelineTriggerKind.Manual,
                IsCi = true,
            },
            ArtifactValidation = new()
            {
                ExpectedSourceRevision = new string('a', 40),
            },
            Actions = new()
            {
                EmitNotifications = true,
            },
            Commits = Enumerable.Range(0, 205)
                .Select(index => new CommitSummary
                {
                    Sha = index.ToString("x40"),
                    Subject = $"Commit {index}",
                })
                .ToArray(),
        };
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = new(Path.Combine(
                root.Directory.FullName,
                "artifacts")),
            SourceRevision = new string('a', 40),
            MsBuildVersion = new()
            {
                TargetFile = versionFile,
                RecoveryFile = new(Path.Combine(
                    root.Directory.FullName,
                    ".recovery.json")),
            },
        };
        var registration = CombineOptionsValidator.Validate(
            root.Directory,
            options,
            inputs,
            new()
            {
                RunFormat = false,
            },
            null);
        var engine = new CombineExecutionEngine(
            registration,
            new PipelineArtifactManifestStore(),
            new NeverProviderFactory(),
            new NuGetPublicationService(
                new NeverCommandExecutor(),
                new NeverSecretResolver()),
            [sink]);

        var result = await engine.ExecuteRunBuildAsync(
                new()
                {
                    Status = PipelineRunStatus.Succeeded,
                },
                CancellationToken.None)
            .ConfigureAwait(false);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Succeeded);
        await Assert.That(sink.Events.Count).IsEqualTo(2);
        await Assert.That(sink.Events[0].RunResult.Status)
            .IsEqualTo(
                TedToolkit.ModularPipelines.Build.Artifacts
                    .PipelineRunStatus.Running);
        await Assert.That(sink.Events[0].RunResult.CompletedAtUtc).IsNull();
        await Assert.That(sink.Events[0].RunResult.Commits.Count)
            .IsEqualTo(200);
        await Assert.That(sink.Events[0].RunResult.CommitsTruncated).IsTrue();
        await Assert.That(sink.Events[1].Kind)
            .IsEqualTo(PipelineEventKind.PipelineCompleted);
        await Assert.That(sink.Events[1].RunResult.CompletedAtUtc).IsNotNull();
    }

    private sealed class NeverProviderFactory : IRepositoryProviderFactory
    {
        public ValueTask<IRepositoryProvider> CreateAsync(
            RepositoryProviderKind kind,
            CombineOptions options,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "A Validate event test cannot resolve a provider.");
        }
    }

    private sealed class NeverCommandExecutor : ICombineCommandExecutor
    {
        public Task<CombineCommandResult> ExecuteAsync(
            CombineCommandRequest request,
            string? sensitiveArgument,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "A Validate event test cannot execute a command.");
        }
    }

    private sealed class NeverSecretResolver : IPipelineSecretResolver
    {
        public ValueTask<string?> ResolveAsync(
            string credentialReference,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "A Validate event test cannot resolve a secret.");
        }
    }

    private sealed class RecordingSink : IPipelineEventSink
    {
        public List<PipelineEvent> Events { get; } = [];

        public Task PublishAsync(
            PipelineEvent pipelineEvent,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(pipelineEvent);
            return Task.CompletedTask;
        }
    }
}