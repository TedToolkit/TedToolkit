// -----------------------------------------------------------------------
// <copyright file="CleanOutputModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Build.Execution;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Recovers interrupted version state, cleans the artifact root, and begins any requested version transaction.
/// </summary>
/// <param name="services">The pipeline service provider.</param>
public sealed class CleanOutputModule(IServiceProvider services)
    : CleanStageModule<bool>
{
    /// <inheritdoc/>
    protected override Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return services.GetRequiredService<BuildPipelineState>()
            .InitializeAsync(cancellationToken);
    }
}