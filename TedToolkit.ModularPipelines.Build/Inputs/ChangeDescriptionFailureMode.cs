// -----------------------------------------------------------------------
// <copyright file="ChangeDescriptionFailureMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents how an optional consumer description-generator failure affects Build.
/// </summary>
public enum ChangeDescriptionFailureMode
{
    /// <summary>
    /// Continues with deterministic behavior and no generated description.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Fails the Build result when an enabled generator fails or returns invalid output.
    /// </summary>
    FailPipeline = 1,
}