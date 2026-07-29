// -----------------------------------------------------------------------
// <copyright file="TemporaryMsBuildVersionOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents the explicit paths and value for recoverable temporary MSBuild version stamping.
/// </summary>
public sealed record TemporaryMsBuildVersionOptions
{
    /// <summary>
    /// Gets the existing root-contained MSBuild XML file to stamp.
    /// </summary>
    public required FileInfo TargetFile { get; init; }

    /// <summary>
    /// Gets the unique XML property name to replace.
    /// </summary>
    public string PropertyName { get; init; } = "Version";

    /// <summary>
    /// Gets the normalized calendar version to apply, or <see langword="null"/> for recovery only.
    /// </summary>
    public string? TargetVersion { get; init; }

    /// <summary>
    /// Gets the root-contained recovery file outside the artifact root.
    /// </summary>
    public required FileInfo RecoveryFile { get; init; }
}