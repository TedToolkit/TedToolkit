// -----------------------------------------------------------------------
// <copyright file="PipelineArtifact.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents one content-addressed file under the configured artifact root.
/// </summary>
public sealed record PipelineArtifact
{
    /// <summary>
    /// Gets the normalized, root-relative path using forward slashes.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Gets the artifact classification.
    /// </summary>
    public required PipelineArtifactKind Kind { get; init; }

    /// <summary>
    /// Gets the stable logical name defined by the producing target.
    /// </summary>
    public required string LogicalName { get; init; }

    /// <summary>
    /// Gets the lowercase SHA-256 digest of the file bytes.
    /// </summary>
    public required string Sha256 { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Gets the nuspec package identifier when this is a package artifact.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the normalized nuspec version when this is a package artifact.
    /// </summary>
    public string? PackageVersion { get; init; }

    /// <summary>
    /// Gets the target framework associated with a publish archive, when specified.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets the runtime identifier associated with a publish archive, when specified.
    /// </summary>
    public string? RuntimeIdentifier { get; init; }
}