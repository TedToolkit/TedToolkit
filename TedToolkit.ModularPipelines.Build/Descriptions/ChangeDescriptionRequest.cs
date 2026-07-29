// -----------------------------------------------------------------------
// <copyright file="ChangeDescriptionRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Descriptions;

/// <summary>
/// Represents bounded local change data explicitly authorized for a consumer generator.
/// </summary>
public sealed record ChangeDescriptionRequest
{
    /// <summary>
    /// Gets the requested description kind.
    /// </summary>
    public required ChangeDescriptionKind Kind { get; init; }

    /// <summary>
    /// Gets the exact source revision.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets the target revision for a change request, when applicable.
    /// </summary>
    public string? TargetRevision { get; init; }

    /// <summary>
    /// Gets normalized repository-relative changed paths.
    /// </summary>
    public required IReadOnlyList<string> ChangedPaths { get; init; }

    /// <summary>
    /// Gets the bounded textual local diff without external diff drivers.
    /// </summary>
    public required string Diff { get; init; }

    /// <summary>
    /// Gets a value indicating whether the diff was truncated at a line boundary.
    /// </summary>
    public bool DiffTruncated { get; init; }

    /// <summary>
    /// Gets the independently composed instructions for this description kind.
    /// </summary>
    public required string Instructions { get; init; }
}