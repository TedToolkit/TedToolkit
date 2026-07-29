// -----------------------------------------------------------------------
// <copyright file="IRepositoryProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Defines provider-neutral operations implemented by one internal adapter.
/// </summary>
internal interface IRepositoryProvider
{
    /// <summary>
    /// Gets the neutral repository context.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The validated context.</returns>
    Task<RepositoryContext> GetContextAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates, updates, or reuses one change request.
    /// </summary>
    /// <param name="request">The neutral request.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The final change request.</returns>
    Task<ChangeRequest> CreateOrUpdateChangeRequestAsync(
        CreateChangeRequestRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes provider-hosted artifacts.
    /// </summary>
    /// <param name="request">The neutral publication request.</param>
    /// <param name="artifactRoot">The validated artifact root.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>The published artifacts.</returns>
    Task<IReadOnlyList<PublishedArtifact>> PublishArtifactsAsync(
        ArtifactPublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a provider-coordinated Release.
    /// </summary>
    /// <param name="request">The neutral Release request.</param>
    /// <param name="artifactRoot">The validated artifact root.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>The final Release result.</returns>
    Task<ReleasePublicationResult> PublishReleaseAsync(
        ReleasePublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes a Release without another artifact action.
    /// </summary>
    /// <param name="request">The empty-artifact neutral request.</param>
    /// <param name="cancellationToken">A token that cancels finalization.</param>
    /// <returns>The final Release result.</returns>
    Task<ReleasePublicationResult> FinalizeReleaseAsync(
        ReleasePublicationRequest request,
        CancellationToken cancellationToken);
}