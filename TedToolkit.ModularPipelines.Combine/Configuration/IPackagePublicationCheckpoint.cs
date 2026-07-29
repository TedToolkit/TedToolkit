// -----------------------------------------------------------------------
// <copyright file="IPackagePublicationCheckpoint.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Defines the optional trusted package-publication checkpoint.
/// </summary>
public interface IPackagePublicationCheckpoint
{
    /// <summary>
    /// Reads package IDs whose immutable completion evidence already matches.
    /// </summary>
    /// <param name="context">The complete immutable context.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>The exact completed package IDs.</returns>
    Task<IReadOnlyList<string>> ReadCompletedPackageIdsAsync(
        PackagePublicationCheckpointContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a confirmed package push before the next push starts.
    /// </summary>
    /// <param name="context">The complete immutable context.</param>
    /// <param name="package">The confirmed package.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>A task that completes after durable recording.</returns>
    Task RecordCompletedAsync(
        PackagePublicationCheckpointContext context,
        PackagePublicationCheckpointRecord package,
        CancellationToken cancellationToken);

    /// <summary>
    /// Confirms the full package set before provider publication.
    /// </summary>
    /// <param name="context">The complete immutable context.</param>
    /// <param name="cancellationToken">A token that cancels the callback.</param>
    /// <returns>A task that completes after the full-set checkpoint.</returns>
    Task CompleteAsync(
        PackagePublicationCheckpointContext context,
        CancellationToken cancellationToken);
}