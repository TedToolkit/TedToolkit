// -----------------------------------------------------------------------
// <copyright file="PipelineConfiguration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains configuration loaded from the two package-owned sections.
/// </summary>
/// <param name="Build">The Build configuration.</param>
/// <param name="Pipeline">The Combine configuration.</param>
public sealed record PipelineConfiguration(
    BuildOptions Build,
    CombineOptions Pipeline);