// -----------------------------------------------------------------------
// <copyright file="BuildInputs.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents all explicit filesystem roots, source identity, and producer targets for Build.
/// </summary>
public sealed record BuildInputs
{
    /// <summary>
    /// Gets the existing absolute consumer root.
    /// </summary>
    public required DirectoryInfo RootDirectory { get; init; }

    /// <summary>
    /// Gets the strict-descendant artifact root.
    /// </summary>
    public required DirectoryInfo ArtifactRootDirectory { get; init; }

    /// <summary>
    /// Gets the exact source revision.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets the required active-profile recovery configuration and optional target version.
    /// </summary>
    public TemporaryMsBuildVersionOptions? MsBuildVersion { get; init; }

    /// <summary>
    /// Gets the declared formatting targets.
    /// </summary>
    public IReadOnlyList<FormatTarget> FormatTargets { get; init; } = [];

    /// <summary>
    /// Gets the declared build targets.
    /// </summary>
    public IReadOnlyList<BuildTarget> BuildTargets { get; init; } = [];

    /// <summary>
    /// Gets the declared test targets.
    /// </summary>
    public IReadOnlyList<TestTarget> TestTargets { get; init; } = [];

    /// <summary>
    /// Gets the declared package targets.
    /// </summary>
    public IReadOnlyList<PackTarget> PackTargets { get; init; } = [];

    /// <summary>
    /// Gets the declared local publish targets.
    /// </summary>
    public IReadOnlyList<DotnetPublishTarget> PublishTargets { get; init; } = [];
}