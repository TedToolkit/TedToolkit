// -----------------------------------------------------------------------
// <copyright file="ConsumeArtifactsCombineModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Modules;

/// <summary>
/// Validates a prior Build manifest and runs the selected Combine actions.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
public sealed class ConsumeArtifactsCombineModule(IServiceProvider services)
    : ReleaseStageModule<PipelineRunResult>
{
    /// <inheritdoc/>
    protected override async Task<PipelineRunResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = await services
            .GetRequiredService<CombineExecutionEngine>()
            .ExecuteConsumeAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result?.Status == PipelineRunStatus.Succeeded)
        {
            return result;
        }

        throw new InvalidOperationException(
            "The artifact-consumption pipeline did not succeed.");
    }
}