// -----------------------------------------------------------------------
// <copyright file="PipelineResourceSource.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents the precedence path that produced a resolved resource document.
/// </summary>
public enum PipelineResourceSource
{
    /// <summary>
    /// Indicates that only the embedded baseline was used.
    /// </summary>
    EmbeddedBase = 0,

    /// <summary>
    /// Indicates that an append-only overlay followed the embedded baseline.
    /// </summary>
    EmbeddedBaseWithOverlay = 1,

    /// <summary>
    /// Indicates that a replacement fully replaced the embedded baseline.
    /// </summary>
    Replacement = 2,
}