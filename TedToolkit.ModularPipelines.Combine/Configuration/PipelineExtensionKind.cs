// -----------------------------------------------------------------------
// <copyright file="PipelineExtensionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Identifies the trusted consumer extension boundary.
/// </summary>
public enum PipelineExtensionKind
{
    /// <summary>
    /// Adds a module to the in-process Build graph.
    /// </summary>
    BuildStage = 0,

    /// <summary>
    /// Adds a module after the Build or manifest boundary.
    /// </summary>
    CombineHook = 1,
}