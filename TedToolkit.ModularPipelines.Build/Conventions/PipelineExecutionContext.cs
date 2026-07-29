// -----------------------------------------------------------------------
// <copyright file="PipelineExecutionContext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Conventions;

/// <summary>
/// Represents explicit provider-neutral execution context supplied by a host.
/// </summary>
public sealed record PipelineExecutionContext
{
    /// <summary>
    /// Gets the neutral trigger category.
    /// </summary>
    public required PipelineTriggerKind Trigger { get; init; }

    /// <summary>
    /// Gets a value indicating whether execution occurs in CI.
    /// </summary>
    public bool IsCi { get; init; }

    /// <summary>
    /// Gets the branch name when the trigger identifies a branch.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the exact source revision.
    /// </summary>
    public string? SourceRevision { get; init; }

    /// <summary>
    /// Gets the prior source revision.
    /// </summary>
    public string? BeforeRevision { get; init; }

    /// <summary>
    /// Gets the safe actor identifier.
    /// </summary>
    public string? Actor { get; init; }

    /// <summary>
    /// Gets the CI run identifier.
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// Gets the canonical CI run URI.
    /// </summary>
    public Uri? RunUri { get; init; }

    /// <summary>
    /// Gets the canonical repository URI.
    /// </summary>
    public Uri? RepositoryUri { get; init; }

    /// <summary>
    /// Gets the canonical comparison URI when one is available.
    /// </summary>
    public Uri? CompareUri { get; init; }
}