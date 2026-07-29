// -----------------------------------------------------------------------
// <copyright file="ValidatedCombineRegistration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Contains the fully validated immutable composition state.
/// </summary>
/// <param name="RootDirectory">The explicit consumer root.</param>
/// <param name="RootPath">The normalized root path.</param>
/// <param name="Profile">The resolved profile.</param>
/// <param name="Context">The neutral execution context.</param>
/// <param name="Options">The authoritative Combine options.</param>
/// <param name="BuildInputs">The optional RunBuild inputs.</param>
/// <param name="BuildOptions">The shared Build options.</param>
/// <param name="ModuleRegistrations">Validated consumer extensions.</param>
/// <param name="ManifestPath">The resolved manifest path.</param>
internal sealed record ValidatedCombineRegistration(
    DirectoryInfo RootDirectory,
    string RootPath,
    PipelineProfile Profile,
    PipelineExecutionContext? Context,
    CombineOptions Options,
    BuildInputs? BuildInputs,
    BuildOptions BuildOptions,
    IReadOnlyList<PipelineModuleRegistration> ModuleRegistrations,
    string? ManifestPath);