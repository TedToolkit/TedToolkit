// -----------------------------------------------------------------------
// <copyright file="ArtifactPublicationRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Represents provider artifact publication without Release creation.
/// </summary>
public sealed record ArtifactPublicationRequest
{
    /// <summary>
    /// Gets the normalized version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets an optional existing Release tag.
    /// </summary>
    public string? TagName { get; init; }

    /// <summary>
    /// Gets validated publish-archive artifacts.
    /// </summary>
    public IReadOnlyList<PipelineArtifact> Artifacts { get; init; } = [];
}