// -----------------------------------------------------------------------
// <copyright file="PackagePublicationCheckpointContext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Represents the trusted package checkpoint context.
/// </summary>
/// <param name="Version">The normalized coordinated version.</param>
/// <param name="SourceRevision">The validated source revision.</param>
/// <param name="ManifestSha256">The manifest hash.</param>
/// <param name="Packages">The immutable package inventory.</param>
public sealed record PackagePublicationCheckpointContext(
    string Version,
    string SourceRevision,
    string ManifestSha256,
    IReadOnlyList<PackagePublicationCheckpointRecord> Packages);