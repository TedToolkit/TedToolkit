// -----------------------------------------------------------------------
// <copyright file="PipelineExecutionPolicy.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects automatic context routing or an explicit profile.
/// </summary>
public enum PipelineExecutionPolicy
{
    /// <summary>
    /// Uses the neutral standard context mapping.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Uses <see cref="CombineOptions.ManualProfile"/>.
    /// </summary>
    Manual = 1,
}