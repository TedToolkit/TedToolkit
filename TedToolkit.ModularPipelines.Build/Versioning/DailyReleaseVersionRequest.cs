// -----------------------------------------------------------------------
// <copyright file="DailyReleaseVersionRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents all caller-selected facts used by the pure daily release policy.
/// </summary>
public sealed record DailyReleaseVersionRequest
{
    /// <summary>
    /// Gets the release calendar date selected by the caller.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// Gets the source revision being versioned.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets the prevalidated consumed version history.
    /// </summary>
    public IReadOnlyList<ConsumedReleaseVersionRecord> ConsumedVersions { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether an earlier publication owns an active reservation.
    /// </summary>
    public bool HasActivePublicationReservation { get; init; }
}