// -----------------------------------------------------------------------
// <copyright file="PipelineResourceWriteResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents an editor resource resolution and its optional atomic write outcome.
/// </summary>
/// <param name="Document">The resolved resource document.</param>
/// <param name="TargetFile">The root-level editor configuration target.</param>
/// <param name="Written">Whether different bytes were atomically committed.</param>
public sealed record PipelineResourceWriteResult(
    PipelineResourceDocument Document,
    FileInfo TargetFile,
    bool Written);