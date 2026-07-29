// -----------------------------------------------------------------------
// <copyright file="PipelineConventions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Conventions;

/// <summary>
/// Provides the immutable standard convention instance.
/// </summary>
public static class PipelineConventions
{
    /// <summary>
    /// Gets the standard main/development/origin convention set.
    /// </summary>
    public static PipelineConventionOptions Standard { get; } = new();
}