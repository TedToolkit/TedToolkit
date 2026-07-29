// -----------------------------------------------------------------------
// <copyright file="RepositoryContext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes a provider-visible repository and run.
/// </summary>
public sealed record RepositoryContext
{
    /// <summary>
    /// Gets the provider's stable repository ID.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Gets the leaf repository display name.
    /// </summary>
    public required string RepositoryName { get; init; }

    /// <summary>
    /// Gets the provider-visible full repository path.
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Gets the canonical repository URI.
    /// </summary>
    public Uri? RepositoryUri { get; init; }

    /// <summary>
    /// Gets the current branch.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the source revision.
    /// </summary>
    public string? SourceRevision { get; init; }

    /// <summary>
    /// Gets the prior revision.
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
    /// Gets the canonical run URI.
    /// </summary>
    public Uri? RunUri { get; init; }

    /// <summary>
    /// Gets the canonical comparison URI.
    /// </summary>
    public Uri? CompareUri { get; init; }

    /// <summary>
    /// Gets whether this is a CI context.
    /// </summary>
    public bool IsCi { get; init; }
}