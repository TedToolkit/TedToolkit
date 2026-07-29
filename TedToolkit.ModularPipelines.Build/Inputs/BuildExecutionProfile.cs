// -----------------------------------------------------------------------
// <copyright file="BuildExecutionProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents the local-only graph selected from the Build package.
/// </summary>
public enum BuildExecutionProfile
{
    /// <summary>
    /// Indicates that Build performs no validation, registration, recovery, or command.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates a developer-local build where explicit repository writes may run.
    /// </summary>
    LocalBuild = 1,

    /// <summary>
    /// Indicates build and test validation with a final manifest.
    /// </summary>
    Validate = 2,

    /// <summary>
    /// Indicates validation followed by explicit package production.
    /// </summary>
    Pack = 3,

    /// <summary>
    /// Indicates package production and local publish-archive production.
    /// </summary>
    Publish = 4,

    /// <summary>
    /// Indicates local validation and optional neutral description generation.
    /// </summary>
    Message = 5,
}