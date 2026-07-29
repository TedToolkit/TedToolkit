// -----------------------------------------------------------------------
// <copyright file="PackagePublicationCheckpointRecord.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Represents immutable publication evidence for one package.
/// </summary>
/// <param name="PackageId">The exact NuGet package ID.</param>
/// <param name="PackageSha256">The primary package hash.</param>
/// <param name="SymbolSha256">The optional sibling symbol-package hash.</param>
public sealed record PackagePublicationCheckpointRecord(
    string PackageId,
    string PackageSha256,
    string? SymbolSha256);