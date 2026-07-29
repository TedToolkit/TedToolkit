// -----------------------------------------------------------------------
// <copyright file="PipelineRunResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Represents the frozen result of one Combine graph.
/// </summary>
public sealed record PipelineRunResult
{
    /// <summary>
    /// Gets the in-process run identifier.
    /// </summary>
    public Guid RunId { get; init; }

    /// <summary>
    /// Gets the resolved profile.
    /// </summary>
    public required PipelineProfile Profile { get; init; }

    /// <summary>
    /// Gets the selected input mode.
    /// </summary>
    public required PipelineInputMode InputMode { get; init; }

    /// <summary>
    /// Gets the terminal status.
    /// </summary>
    public required PipelineRunStatus Status { get; init; }

    /// <summary>
    /// Gets the start timestamp.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the terminal timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets the provider-neutral repository context.
    /// </summary>
    public RepositoryContext? Repository { get; init; }

    /// <summary>
    /// Gets the bounded commit summary.
    /// </summary>
    public IReadOnlyList<CommitSummary> Commits { get; init; } = [];

    /// <summary>
    /// Gets whether older commits were omitted.
    /// </summary>
    public bool CommitsTruncated { get; init; }

    /// <summary>
    /// Gets the in-process or reconstructed Build result.
    /// </summary>
    public BuildResult? BuildResult { get; init; }

    /// <summary>
    /// Gets the completed change-request operation.
    /// </summary>
    public ChangeRequest? ChangeRequest { get; init; }

    /// <summary>
    /// Gets the completed Release operation.
    /// </summary>
    public ReleasePublicationResult? ReleasePublication { get; init; }

    /// <summary>
    /// Gets NuGet package outcomes.
    /// </summary>
    public IReadOnlyList<PackagePushResult> PackagePushes { get; init; } = [];

    /// <summary>
    /// Gets provider-hosted publish archives.
    /// </summary>
    public IReadOnlyList<PublishedArtifact> PublishedArtifacts { get; init; } = [];

    /// <summary>
    /// Gets safe failures.
    /// </summary>
    public IReadOnlyList<PipelineFailure> Failures { get; init; } = [];
}