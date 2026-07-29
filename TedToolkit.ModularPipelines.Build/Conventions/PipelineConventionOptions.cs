// -----------------------------------------------------------------------
// <copyright file="PipelineConventionOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Conventions;

/// <summary>
/// Represents overridable branch, remote, and directory conventions.
/// </summary>
public sealed record PipelineConventionOptions
{
    /// <summary>
    /// Gets the main branch name.
    /// </summary>
    public string MainBranch { get; init; } = "main";

    /// <summary>
    /// Gets the development branch name.
    /// </summary>
    public string DevelopmentBranch { get; init; } = "development";

    /// <summary>
    /// Gets the local Git remote name.
    /// </summary>
    public string GitRemoteName { get; init; } = "origin";

    /// <summary>
    /// Gets the portable pipeline layout.
    /// </summary>
    public PipelineLayout Layout { get; init; } = new();
}