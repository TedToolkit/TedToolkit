// -----------------------------------------------------------------------
// <copyright file="CollectPublishArtifactsModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.ModularPipelines.Build.Execution;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Inventories durable artifacts before temporary version state is restored.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<DotnetPublishModule>]
public sealed class CollectPublishArtifactsModule(IServiceProvider services)
    : Module<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .CollectAsync();
    }
}