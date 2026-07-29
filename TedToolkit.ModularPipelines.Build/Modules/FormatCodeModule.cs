// -----------------------------------------------------------------------
// <copyright file="FormatCodeModule.cs" company="TedToolkit">
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
/// Formats only the explicitly selected local path scope.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
[DependsOn<UpdateEditorConfigModule>]
public sealed class FormatCodeModule(IServiceProvider services)
    : PrepareStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .FormatAsync(cancellationToken);
    }
}