// -----------------------------------------------------------------------
// <copyright file="ReleasePublicationRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Represents one provider-coordinated Release operation.
/// </summary>
public sealed record ReleasePublicationRequest
{
    /// <summary>
    /// Gets the normalized publication version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the exact tag.
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Gets the validated source revision.
    /// </summary>
    public required string TargetRevision { get; init; }

    /// <summary>
    /// Gets the Release title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the normalized Release body.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets whether the normalized version is prerelease.
    /// </summary>
    public bool IsPrerelease { get; init; }

    /// <summary>
    /// Gets validated publish-archive artifacts.
    /// </summary>
    public IReadOnlyList<PipelineArtifact> Artifacts { get; init; } = [];
}