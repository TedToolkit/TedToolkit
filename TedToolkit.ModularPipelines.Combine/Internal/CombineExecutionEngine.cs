// -----------------------------------------------------------------------
// <copyright file="CombineExecutionEngine.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Events;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Coordinates validated Combine actions and immutable event snapshots.
/// </summary>
/// <param name="registration">The validated immutable registration.</param>
/// <param name="manifestReader">The strict Build manifest reader.</param>
/// <param name="providerFactory">The selected-provider factory.</param>
/// <param name="nuGetPublication">The NuGet publication service.</param>
/// <param name="sinks">The optional neutral event sinks.</param>
internal sealed class CombineExecutionEngine(
    ValidatedCombineRegistration registration,
    IPipelineArtifactManifestReader manifestReader,
    IRepositoryProviderFactory providerFactory,
    NuGetPublicationService nuGetPublication,
    IEnumerable<IPipelineEventSink> sinks)
{
    private const int MaximumCommitCount = 200;

    private readonly Guid _runId = Guid.NewGuid();

    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    private readonly List<PipelineFailure> _failures = [];

    private readonly List<PackagePushResult> _packagePushes = [];

    private readonly List<PublishedArtifact> _publishedArtifacts = [];

    private ChangeRequest? _changeRequest;

    private ReleasePublicationResult? _releasePublication;

    private RepositoryContext? _repository;

    private BuildResult? _buildResult;

    private PipelineArtifactManifest? _manifest;

    /// <summary>
    /// Executes Combine after an in-process Build result.
    /// </summary>
    /// <param name="buildResult">The finalized Build result.</param>
    /// <param name="cancellationToken">A token that cancels work.</param>
    /// <returns>The frozen Combine result.</returns>
    public async Task<PipelineRunResult> ExecuteRunBuildAsync(
        BuildResult buildResult,
        CancellationToken cancellationToken)
    {
        _buildResult = buildResult;

        if (registration.ManifestPath is not null
            && File.Exists(registration.ManifestPath))
        {
            _manifest = await manifestReader.ReadAsync(
                    registration.RootDirectory,
                    new FileInfo(registration.ManifestPath),
                    registration.Options.ArtifactValidation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await ExecuteAfterInputAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a prior manifest and executes only Combine actions.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels work.</param>
    /// <returns>The frozen Combine result.</returns>
    public async Task<PipelineRunResult?> ExecuteConsumeAsync(
        CancellationToken cancellationToken)
    {
        _manifest = await manifestReader.ReadAsync(
                registration.RootDirectory,
                new FileInfo(registration.ManifestPath!),
                registration.Options.ArtifactValidation,
                cancellationToken)
            .ConfigureAwait(false);
        _buildResult = new()
        {
            Status = _manifest.Status,
            Targets = _manifest.Targets,
            Tests = _manifest.Tests,
            Artifacts = _manifest.Artifacts,
            Failures = _manifest.Failures,
        };
        return await ExecuteAfterInputAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PipelineRunResult> ExecuteAfterInputAsync(
        CancellationToken cancellationToken)
    {
        if (_buildResult!.Status == PipelineRunStatus.Failed)
        {
            _failures.AddRange(_buildResult.Failures);
            return await CompleteFailedAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (!await DispatchAsync(
                PipelineEventKind.BuildValidated,
                terminal: false,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return await CompleteFailedAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            if (registration.Options.Actions.PushNuGetPackages)
            {
                EnsureMutationInput();
                var artifactRoot = new FileInfo(registration.ManifestPath!)
                    .Directory!;
                _packagePushes.AddRange(
                    await nuGetPublication.PublishAsync(
                            registration,
                            _manifest!,
                            artifactRoot,
                            cancellationToken)
                        .ConfigureAwait(false));
            }

            if (registration.Options.Actions.CreateChangeRequest)
            {
                var provider = await GetProviderAsync(cancellationToken)
                    .ConfigureAwait(false);
                _repository ??= await provider.GetContextAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                _changeRequest =
                    await provider.CreateOrUpdateChangeRequestAsync(
                            CreateChangeRequest(),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!await DispatchAsync(
                        PipelineEventKind.ChangeRequestCompleted,
                        terminal: false,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    return await CompleteFailedAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (registration.Options.Actions.PublishArtifacts
                || registration.Options.Actions.CreateRelease)
            {
                EnsureMutationInput();
                var provider = await GetProviderAsync(cancellationToken)
                    .ConfigureAwait(false);
                _repository ??= await provider.GetContextAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                var artifactRoot = new FileInfo(registration.ManifestPath!)
                    .Directory!;

                if (registration.Options.Actions.CreateRelease)
                {
                    _releasePublication =
                        await provider.PublishReleaseAsync(
                                CreateReleaseRequest(
                                    registration.Options.Actions
                                        .PublishArtifacts),
                                artifactRoot,
                                cancellationToken)
                            .ConfigureAwait(false);
                    _publishedArtifacts.AddRange(
                        _releasePublication.Artifacts);
                }
                else
                {
                    _publishedArtifacts.AddRange(
                        await provider.PublishArtifactsAsync(
                                CreateArtifactRequest(),
                                artifactRoot,
                                cancellationToken)
                            .ConfigureAwait(false));
                }
            }

            if ((registration.Options.Actions.PushNuGetPackages
                    || registration.Options.Actions.PublishArtifacts)
                && !await DispatchAsync(
                        PipelineEventKind.PublicationCompleted,
                        terminal: false,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return await CompleteFailedAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (registration.Options.Actions.CreateRelease
                && !await DispatchAsync(
                        PipelineEventKind.ReleaseCompleted,
                        terminal: false,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return await CompleteFailedAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or UnauthorizedAccessException
            or TimeoutException
            or OperationCanceledException)
        {
            _failures.Add(new PipelineFailure()
            {
                Code = exception is OperationCanceledException
                    ? "COMBINE_CANCELLED"
                    : "COMBINE_OPERATION_FAILED",
                Stage = "Combine",
                Summary =
                    "An explicitly enabled Combine operation did not complete.",
            });
            return await CompleteFailedAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }

        var successful = CreateSnapshot(
            PipelineRunStatus.Succeeded,
            terminal: true);
        _ = await DispatchAsync(
                PipelineEventKind.PipelineCompleted,
                terminal: true,
                cancellationToken)
            .ConfigureAwait(false);
        return successful;
    }

    private async Task<PipelineRunResult> CompleteFailedAsync(
        CancellationToken cancellationToken)
    {
        var result = CreateSnapshot(
            PipelineRunStatus.Failed,
            terminal: true);
        _ = await DispatchAsync(
                PipelineEventKind.PipelineFailed,
                terminal: true,
                cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private async Task<bool> DispatchAsync(
        PipelineEventKind kind,
        bool terminal,
        CancellationToken cancellationToken)
    {
        if (!registration.Options.Actions.EmitNotifications)
        {
            return true;
        }

        var eventSinks = sinks.ToArray();

        if (eventSinks.Length == 0)
        {
            return true;
        }

        PipelineRunStatus status;
        if (kind == PipelineEventKind.PipelineFailed)
        {
            status = PipelineRunStatus.Failed;
        }
        else if (_failures.Count == 0)
        {
            status = PipelineRunStatus.Succeeded;
        }
        else
        {
            status = PipelineRunStatus.Failed;
        }

        var pipelineEvent = new PipelineEvent()
        {
            Kind = kind,
            RunResult = CreateSnapshot(
                terminal ? status : PipelineRunStatus.Running,
                terminal),
            ChangeRequest = kind == PipelineEventKind.ChangeRequestCompleted
                ? _changeRequest
                : null,
            Release = kind == PipelineEventKind.ReleaseCompleted
                ? _releasePublication?.Release
                : null,
            Failure = kind == PipelineEventKind.PipelineFailed
                ? _failures.LastOrDefault()
                : null,
        };

        foreach (var sink in eventSinks)
        {
            if (await TryPublishSinkAsync(
                    sink,
                    pipelineEvent,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            if (!terminal
                && registration.Options.Actions.NotificationFailureMode
                == NotificationFailureMode.FailPipeline)
            {
                _failures.Add(new PipelineFailure()
                {
                    Code = "EVENT_SINK_FAILED",
                    Stage = "Notifications",
                    Summary =
                        "A configured event sink did not complete.",
                });
                return false;
            }
        }

        return true;
    }

    private async Task<bool> TryPublishSinkAsync(
        IPipelineEventSink sink,
        PipelineEvent pipelineEvent,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var delivery = Task.Run(
            () => sink.PublishAsync(pipelineEvent, linked.Token),
            CancellationToken.None);
        var observed = delivery.ContinueWith(
            task =>
            {
                _ = task.Exception;
                return task.Status == TaskStatus.RanToCompletion;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            return await observed.WaitAsync(
                    registration.Options.Actions.NotificationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await linked.CancelAsync().ConfigureAwait(false);
            return false;
        }
    }

    private ValueTask<IRepositoryProvider> GetProviderAsync(
        in CancellationToken cancellationToken)
    {
        return providerFactory.CreateAsync(
                registration.Options.RepositoryProvider!.Value,
                registration.Options,
                cancellationToken);
    }

    private void EnsureMutationInput()
    {
        if (_manifest is null
            || _manifest.Status != PipelineRunStatus.Succeeded
            || _manifest.SourceTreeDirty
            || string.IsNullOrWhiteSpace(_manifest.SourceRevision)
            || _manifest.Targets.Any(target => !target.Succeeded)
            || _manifest.Tests.Any(test => !test.Succeeded))
        {
            throw new InvalidDataException(
                "Remote mutation requires a clean successful manifest.");
        }

        var configured = registration.Options.Publication?.Version;

        if (configured is null
            || _manifest.PackageVersion is null
            || NuGetVersion.Parse(configured).Equals(
                NuGetVersion.Parse(_manifest.PackageVersion)))
        {
            return;
        }

        throw new InvalidDataException(
            "Publication and manifest package versions differ.");
    }

    private CreateChangeRequestRequest CreateChangeRequest()
    {
        var context = registration.Context
            ?? throw new InvalidDataException(
                "Message requires an execution context.");
        var source = context.Branch
            ?? throw new InvalidDataException(
                "Message requires a source branch.");
        var conventions = registration.BuildOptions.Conventions;

        if (source.Equals(
                conventions.MainBranch,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Message cannot originate from the main branch.");
        }

        var target = source.Equals(
            conventions.DevelopmentBranch,
            StringComparison.Ordinal)
            ? conventions.MainBranch
            : conventions.DevelopmentBranch;
        var description = _buildResult?.GeneratedDescription;
        var title = description?.Subject
                    ?? (source.Equals(
                            conventions.DevelopmentBranch,
                            StringComparison.Ordinal)
                        ? registration.Options.ChangeRequest.ReleaseTitle
                        : $"Merge {source} into {target}");
        var labels = registration.Options.ChangeRequest.Labels
            .Select(label => label.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new()
        {
            SourceBranch = source,
            TargetBranch = target,
            Title = title.Trim(),
            Body = description?.Body?.ReplaceLineEndings("\n")
                   ?? "",
            Draft = registration.Options.ChangeRequest.Draft,
            Labels = labels,
            PreserveIssueClosingDirectives = registration.Options.ChangeRequest
                .PreserveIssueClosingDirectives,
        };
    }

    private ArtifactPublicationRequest CreateArtifactRequest()
    {
        var publication = registration.Options.Publication
            ?? throw new InvalidDataException(
                "Publication options are required.");
        var configuredVersion = publication.Version
            ?? throw new InvalidDataException(
                "Publication version is required.");
        return new()
        {
            Version = NuGetVersion.Parse(configuredVersion)
                .ToNormalizedString(),
            TagName = publication.TagName?.Trim(),
            Artifacts = _manifest!.Artifacts
                .Where(artifact =>
                    artifact.Kind == PipelineArtifactKind.PublishArchive)
                .ToArray(),
        };
    }

    private ReleasePublicationRequest CreateReleaseRequest(
        bool includeArtifacts)
    {
        var publication = registration.Options.Publication
            ?? throw new InvalidDataException(
                "Publication options are required.");
        var version = NuGetVersion.Parse(
            publication.Version
            ?? throw new InvalidDataException(
                "Publication version is required."));
        return new()
        {
            Version = version.ToNormalizedString(),
            TagName = publication.TagName!.Trim(),
            TargetRevision = _manifest!.SourceRevision,
            Title = publication.Title!.Trim(),
            Body = publication.Body.ReplaceLineEndings("\n"),
            IsPrerelease = version.IsPrerelease,
            Artifacts = includeArtifacts
                ? _manifest.Artifacts
                    .Where(artifact =>
                        artifact.Kind
                        == PipelineArtifactKind.PublishArchive)
                    .ToArray()
                : [],
        };
    }

    private PipelineRunResult CreateSnapshot(
        PipelineRunStatus status,
        bool terminal)
    {
        return new()
        {
            RunId = _runId,
            Profile = registration.Profile,
            InputMode = registration.Options.InputMode,
            Status = status,
            StartedAtUtc = _startedAtUtc,
            CompletedAtUtc = terminal
                ? DateTimeOffset.UtcNow
                : null,
            Repository = _repository,
            Commits = registration.Options.Commits
                .Take(MaximumCommitCount)
                .ToArray(),
            CommitsTruncated = registration.Options.Commits.Count
                               > MaximumCommitCount,
            BuildResult = _buildResult,
            ChangeRequest = _changeRequest,
            ReleasePublication = _releasePublication,
            PackagePushes = _packagePushes.ToArray(),
            PublishedArtifacts = _publishedArtifacts.ToArray(),
            Failures = _failures.ToArray(),
        };
    }
}