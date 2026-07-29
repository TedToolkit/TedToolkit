// -----------------------------------------------------------------------
// <copyright file="IPipelineResourceComposer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents deterministic resource composition and explicitly authorized editor writing.
/// </summary>
public interface IPipelineResourceComposer
{
    /// <summary>
    /// Resolves the editor document without writing it.
    /// </summary>
    /// <param name="rootDirectory">The explicit existing consumer root.</param>
    /// <param name="options">The resource precedence options.</param>
    /// <param name="cancellationToken">A token that cancels override reads.</param>
    /// <returns>The normalized resolved editor document.</returns>
    Task<PipelineResourceDocument> GetEditorConfigAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves commit-message instructions without writing them.
    /// </summary>
    /// <param name="rootDirectory">The explicit existing consumer root.</param>
    /// <param name="options">The resource precedence options.</param>
    /// <param name="cancellationToken">A token that cancels override reads.</param>
    /// <returns>The normalized resolved commit instructions.</returns>
    Task<PipelineResourceDocument> GetCommitMessageInstructionsAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves change-request instructions without writing them.
    /// </summary>
    /// <param name="rootDirectory">The explicit existing consumer root.</param>
    /// <param name="options">The resource precedence options.</param>
    /// <param name="cancellationToken">A token that cancels override reads.</param>
    /// <returns>The normalized resolved change-request instructions.</returns>
    Task<PipelineResourceDocument> GetChangeRequestInstructionsAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves and, only when authorized, atomically writes the root editor configuration.
    /// </summary>
    /// <param name="rootDirectory">The explicit existing consumer root.</param>
    /// <param name="options">The resource precedence and write options.</param>
    /// <param name="cancellationToken">A token that cancels reads and writing.</param>
    /// <returns>The resolved document, target, and whether bytes changed.</returns>
    Task<PipelineResourceWriteResult> WriteEditorConfigAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default);
}