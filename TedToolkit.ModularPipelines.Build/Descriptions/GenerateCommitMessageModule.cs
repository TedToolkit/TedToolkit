// -----------------------------------------------------------------------
// <copyright file="GenerateCommitMessageModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Modules;

namespace TedToolkit.ModularPipelines.Build.Descriptions;

/// <summary>
/// Invokes an optional neutral change-description generator without persisting its output.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<AssertBuildTestModule>]
public sealed class GenerateCommitMessageModule(IServiceProvider services)
    : CheckStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .DescribeAsync(cancellationToken);
    }
}