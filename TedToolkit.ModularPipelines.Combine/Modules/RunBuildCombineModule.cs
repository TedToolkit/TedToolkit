// -----------------------------------------------------------------------
// <copyright file="RunBuildCombineModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Modules;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Modules;

/// <summary>
/// Runs Combine after the in-process Build result has been finalized.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<WriteArtifactManifestModule>]
public sealed class RunBuildCombineModule(IServiceProvider services)
    : ReleaseStageModule<PipelineRunResult>
{
    /// <inheritdoc/>
    protected override async Task<PipelineRunResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var buildResult = await services
            .GetRequiredService<BuildPipelineState>()
            .FinishAsync()
            .ConfigureAwait(false);
        var result = await services
            .GetRequiredService<CombineExecutionEngine>()
            .ExecuteRunBuildAsync(
                buildResult,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == PipelineRunStatus.Succeeded)
        {
            return result;
        }

        throw new InvalidOperationException(
            "The composed Build pipeline did not succeed.");
    }
}