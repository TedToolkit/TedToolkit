// -----------------------------------------------------------------------
// <copyright file="Release.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes a repository Release.
/// </summary>
public sealed record Release
{
    /// <summary>
    /// Gets the exact tag.
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Gets the final title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the canonical Release URI.
    /// </summary>
    public required Uri Uri { get; init; }

    /// <summary>
    /// Gets whether the Release remains a draft.
    /// </summary>
    public bool IsDraft { get; init; }

    /// <summary>
    /// Gets whether the Release is prerelease.
    /// </summary>
    public bool IsPrerelease { get; init; }

    /// <summary>
    /// Gets the idempotent operation disposition.
    /// </summary>
    public required OperationDisposition Disposition { get; init; }
}