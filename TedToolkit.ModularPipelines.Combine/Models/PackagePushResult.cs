// -----------------------------------------------------------------------
// <copyright file="PackagePushResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes one NuGet package attempt.
/// </summary>
public sealed record PackagePushResult
{
    /// <summary>
    /// Gets the exact package ID.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets the normalized package version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the canonical package source.
    /// </summary>
    public required Uri Source { get; init; }

    /// <summary>
    /// Gets the command disposition.
    /// </summary>
    public required OperationDisposition Disposition { get; init; }
}