// -----------------------------------------------------------------------
// <copyright file="PipelineRunStatus.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents the terminal status persisted in a Build artifact manifest.
/// </summary>
public enum PipelineRunStatus
{
    /// <summary>
    /// Indicates an in-process run that has not reached a terminal boundary.
    /// </summary>
    Running = 0,

    /// <summary>
    /// Indicates that every configured producer and verification succeeded.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// Indicates that at least one configured operation failed or was cancelled.
    /// </summary>
    Failed = 2,
}