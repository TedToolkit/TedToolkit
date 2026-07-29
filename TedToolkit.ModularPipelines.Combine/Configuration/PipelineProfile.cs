// -----------------------------------------------------------------------
// <copyright file="PipelineProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Identifies a fixed Combine execution graph.
/// </summary>
public enum PipelineProfile
{
    /// <summary>
    /// Runs local build behavior.
    /// </summary>
    LocalBuild = 0,

    /// <summary>
    /// Runs or consumes validation behavior.
    /// </summary>
    Validate = 1,

    /// <summary>
    /// Runs explicit package production.
    /// </summary>
    Pack = 2,

    /// <summary>
    /// Runs or consumes local artifacts and optional publication.
    /// </summary>
    Publish = 3,

    /// <summary>
    /// Runs message and optional change-request behavior.
    /// </summary>
    Message = 4,

    /// <summary>
    /// Performs no package-owned operation.
    /// </summary>
    None = 5,
}