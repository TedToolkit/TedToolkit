// -----------------------------------------------------------------------
// <copyright file="ReleaseVersionResolution.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents a provider-neutral release version decision.
/// </summary>
public sealed record ReleaseVersionResolution
{
    /// <summary>
    /// Gets the resolution category.
    /// </summary>
    public required ReleaseVersionResolutionKind Kind { get; init; }

    /// <summary>
    /// Gets the normalized buildable version when <see cref="Kind"/> is <see cref="ReleaseVersionResolutionKind.NewVersion"/>.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the allocated daily counter when a new version was produced.
    /// </summary>
    public int? Counter { get; init; }
}