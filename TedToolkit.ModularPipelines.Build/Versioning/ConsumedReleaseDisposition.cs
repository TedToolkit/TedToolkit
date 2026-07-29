// -----------------------------------------------------------------------
// <copyright file="ConsumedReleaseDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents why a calendar release version is permanently consumed.
/// </summary>
public enum ConsumedReleaseDisposition
{
    /// <summary>
    /// Indicates that the complete package set succeeded.
    /// </summary>
    PackageSucceeded = 0,

    /// <summary>
    /// Indicates that an unsafe or partial attempt was explicitly abandoned.
    /// </summary>
    Abandoned = 1,
}