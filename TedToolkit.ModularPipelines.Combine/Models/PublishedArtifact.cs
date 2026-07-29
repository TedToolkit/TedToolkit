// -----------------------------------------------------------------------
// <copyright file="PublishedArtifact.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes one published remote artifact.
/// </summary>
public sealed record PublishedArtifact
{
    /// <summary>
    /// Gets the consumer-supplied logical name.
    /// </summary>
    public required string LogicalName { get; init; }

    /// <summary>
    /// Gets the published filename.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the canonical remote URI.
    /// </summary>
    public required Uri Uri { get; init; }

    /// <summary>
    /// Gets the validated lowercase SHA-256.
    /// </summary>
    public required string Sha256 { get; init; }

    /// <summary>
    /// Gets the byte size.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets an optional media type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the idempotent operation disposition.
    /// </summary>
    public required OperationDisposition Disposition { get; init; }
}