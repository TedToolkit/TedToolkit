// -----------------------------------------------------------------------
// <copyright file="BuildPipelineRegistration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents the validated immutable configuration registered for one pipeline execution.
/// </summary>
/// <param name="Profile">The selected execution profile.</param>
/// <param name="Inputs">The explicit Build inputs.</param>
/// <param name="Options">The resolved Build options.</param>
internal sealed record BuildPipelineRegistration(
    BuildExecutionProfile Profile,
    BuildInputs Inputs,
    BuildOptions Options);