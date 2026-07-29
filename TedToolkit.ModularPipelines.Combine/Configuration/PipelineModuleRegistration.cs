// -----------------------------------------------------------------------
// <copyright file="PipelineModuleRegistration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Describes one trusted profile-scoped consumer extension.
/// </summary>
/// <param name="Profiles">The nonempty active profile set.</param>
/// <param name="Kind">The extension boundary.</param>
/// <param name="WritesRepositoryFiles">Whether the extension writes repository files.</param>
/// <param name="Configure">The trusted builder callback.</param>
public sealed record PipelineModuleRegistration(
    IReadOnlySet<PipelineProfile> Profiles,
    PipelineExtensionKind Kind,
    bool WritesRepositoryFiles,
    Action<PipelineBuilder> Configure);