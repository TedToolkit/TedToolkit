// -----------------------------------------------------------------------
// <copyright file="ConsumedReleaseVersionRecord.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents one provider-neutral release version that must not be reused.
/// </summary>
public sealed record ConsumedReleaseVersionRecord
{
    /// <summary>
    /// Gets the normalized calendar package version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the source revision associated with the consumed version.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets the disposition that consumed the version.
    /// </summary>
    public required ConsumedReleaseDisposition Disposition { get; init; }
}