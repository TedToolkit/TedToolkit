// -----------------------------------------------------------------------
// <copyright file="DotnetBuildModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Execution;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Cleans and builds the explicitly declared .NET targets.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
public sealed class DotnetBuildModule(IServiceProvider services)
    : CompileStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .BuildAsync(cancellationToken);
    }
}