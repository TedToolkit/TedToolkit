// -----------------------------------------------------------------------
// <copyright file="IPipelineArtifactManifestWriter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents an atomic writer for validated artifact manifests.
/// </summary>
public interface IPipelineArtifactManifestWriter
{
    /// <summary>
    /// Validates and atomically commits one manifest directly under an artifact root.
    /// </summary>
    /// <param name="artifactRoot">The explicit artifact root.</param>
    /// <param name="manifestFileName">The safe root-level manifest filename.</param>
    /// <param name="manifest">The fully populated manifest.</param>
    /// <param name="cancellationToken">A token that cancels validation and writing.</param>
    /// <returns>The committed manifest file.</returns>
    Task<FileInfo> WriteAsync(
        DirectoryInfo artifactRoot,
        string manifestFileName,
        PipelineArtifactManifest manifest,
        CancellationToken cancellationToken = default);
}