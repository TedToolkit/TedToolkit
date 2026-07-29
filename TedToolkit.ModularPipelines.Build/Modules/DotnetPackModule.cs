// -----------------------------------------------------------------------
// <copyright file="DotnetPackModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Execution;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Produces the explicitly declared NuGet packages without remote publication.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<GenerateCommitMessageModule>]
public sealed class DotnetPackModule(IServiceProvider services)
    : CheckStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .PackAsync(cancellationToken);
    }
}