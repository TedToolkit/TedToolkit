// -----------------------------------------------------------------------
// <copyright file="PipelineArtifactManifest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents the exact schema-1 cross-job artifact handoff.
/// </summary>
public sealed record PipelineArtifactManifest
{
    /// <summary>
    /// Gets the manifest schema version.
    /// </summary>
    /// <value>The integer <c>1</c>.</value>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the terminal Build status.
    /// </summary>
    public required PipelineRunStatus Status { get; init; }

    /// <summary>
    /// Gets the exact source revision from which the artifacts were produced.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets a value indicating whether non-artifact source changes existed at capture time.
    /// </summary>
    public required bool SourceTreeDirty { get; init; }

    /// <summary>
    /// Gets the UTC instant at which the manifest model was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the shared normalized package version, or <see langword="null"/> when no primary package exists.
    /// </summary>
    public string? PackageVersion { get; init; }

    /// <summary>
    /// Gets the deterministic target-result snapshot.
    /// </summary>
    public IReadOnlyList<TargetExecutionResult> Targets { get; init; } = [];

    /// <summary>
    /// Gets the deterministic test-result snapshot.
    /// </summary>
    public IReadOnlyList<TestExecutionResult> Tests { get; init; } = [];

    /// <summary>
    /// Gets the complete artifact inventory, excluding the manifest itself.
    /// </summary>
    public IReadOnlyList<PipelineArtifact> Artifacts { get; init; } = [];

    /// <summary>
    /// Gets the redacted failure snapshot.
    /// </summary>
    public IReadOnlyList<PipelineFailure> Failures { get; init; } = [];
}