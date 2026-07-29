// -----------------------------------------------------------------------
// <copyright file="WriteArtifactManifestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Restores temporary version state and writes the final artifact manifest when required.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<CollectPublishArtifactsModule>]
public sealed class WriteArtifactManifestModule(IServiceProvider services)
    : Module<BuildResult>
{
    /// <inheritdoc/>
    protected override async Task<BuildResult?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (BuildResult?)await services
            .GetRequiredService<BuildPipelineState>()
            .FinishAsync()
            .ConfigureAwait(false);
    }
}