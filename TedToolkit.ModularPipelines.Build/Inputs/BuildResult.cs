// -----------------------------------------------------------------------
// <copyright file="BuildResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Descriptions;

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents the immutable same-process outcome of a Build execution.
/// </summary>
public sealed record BuildResult
{
    /// <summary>
    /// Gets the terminal status.
    /// </summary>
    public required PipelineRunStatus Status { get; init; }

    /// <summary>
    /// Gets deterministic non-test target results.
    /// </summary>
    public IReadOnlyList<TargetExecutionResult> Targets { get; init; } = [];

    /// <summary>
    /// Gets deterministic test target results.
    /// </summary>
    public IReadOnlyList<TestExecutionResult> Tests { get; init; } = [];

    /// <summary>
    /// Gets the complete typed artifact snapshot.
    /// </summary>
    public IReadOnlyList<PipelineArtifact> Artifacts { get; init; } = [];

    /// <summary>
    /// Gets redacted failures.
    /// </summary>
    public IReadOnlyList<PipelineFailure> Failures { get; init; } = [];

    /// <summary>
    /// Gets an optional generated description that is never persisted in the manifest.
    /// </summary>
    public ChangeDescription? GeneratedDescription { get; init; }
}