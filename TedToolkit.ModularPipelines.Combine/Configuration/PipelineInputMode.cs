// -----------------------------------------------------------------------
// <copyright file="PipelineInputMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects in-process Build execution or cross-job manifest consumption.
/// </summary>
public enum PipelineInputMode
{
    /// <summary>
    /// Runs the Build graph in the current process.
    /// </summary>
    RunBuild = 0,

    /// <summary>
    /// Reads a completed Build artifact manifest.
    /// </summary>
    ConsumeArtifacts = 1,
}