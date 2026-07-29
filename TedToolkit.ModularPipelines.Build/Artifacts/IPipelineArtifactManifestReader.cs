// -----------------------------------------------------------------------
// <copyright file="IPipelineArtifactManifestReader.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents a strict reader and validator for cross-job artifact manifests.
/// </summary>
public interface IPipelineArtifactManifestReader
{
    /// <summary>
    /// Reads and validates one root-contained manifest and every referenced artifact.
    /// </summary>
    /// <param name="artifactRoot">The explicit artifact root.</param>
    /// <param name="manifestFile">The root-level manifest file.</param>
    /// <param name="options">Optional source-revision validation settings.</param>
    /// <param name="cancellationToken">A token that cancels asynchronous file and hash operations.</param>
    /// <returns>The validated schema-1 manifest.</returns>
    Task<PipelineArtifactManifest> ReadAsync(
        DirectoryInfo artifactRoot,
        FileInfo manifestFile,
        ArtifactValidationOptions? options = null,
        CancellationToken cancellationToken = default);
}