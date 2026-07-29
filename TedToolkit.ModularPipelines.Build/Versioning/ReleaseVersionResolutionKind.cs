// -----------------------------------------------------------------------
// <copyright file="ReleaseVersionResolutionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents the outcome category of daily release version resolution.
/// </summary>
public enum ReleaseVersionResolutionKind
{
    /// <summary>
    /// Indicates that a new buildable version was allocated.
    /// </summary>
    NewVersion = 0,

    /// <summary>
    /// Indicates that an active publication must be recovered before a new build.
    /// </summary>
    PublicationRecoveryRequired = 1,

    /// <summary>
    /// Indicates that the assembly-compatible daily counter range is exhausted.
    /// </summary>
    Exhausted = 2,
}