// -----------------------------------------------------------------------
// <copyright file="CommitSummary.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes one bounded commit-summary item.
/// </summary>
public sealed record CommitSummary
{
    /// <summary>
    /// Gets the commit object ID.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets the normalized one-line subject.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the normalized author display value.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the author timestamp in UTC.
    /// </summary>
    public DateTimeOffset? AuthoredAtUtc { get; init; }

    /// <summary>
    /// Gets the classified insertion count.
    /// </summary>
    public long? Insertions { get; init; }

    /// <summary>
    /// Gets the classified deletion count.
    /// </summary>
    public long? Deletions { get; init; }

    /// <summary>
    /// Gets the canonical commit URI.
    /// </summary>
    public Uri? Uri { get; init; }
}