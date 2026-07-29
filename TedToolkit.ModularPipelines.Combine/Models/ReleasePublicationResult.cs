// -----------------------------------------------------------------------
// <copyright file="ReleasePublicationResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Represents a completed provider-coordinated Release operation.
/// </summary>
public sealed record ReleasePublicationResult
{
    /// <summary>
    /// Gets the final Release.
    /// </summary>
    public required Release Release { get; init; }

    /// <summary>
    /// Gets published artifacts in deterministic order.
    /// </summary>
    public IReadOnlyList<PublishedArtifact> Artifacts { get; init; } = [];
}