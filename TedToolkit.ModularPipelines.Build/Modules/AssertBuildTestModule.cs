// -----------------------------------------------------------------------
// <copyright file="AssertBuildTestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Execution;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Exposes the build-and-test failure gate to dependent producer modules.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<TestModule>]
public sealed class AssertBuildTestModule(IServiceProvider services)
    : CheckStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(
            services.GetRequiredService<BuildPipelineState>()
                .CanContinue);
    }
}