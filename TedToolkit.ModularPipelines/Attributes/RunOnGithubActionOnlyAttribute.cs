// -----------------------------------------------------------------------
// <copyright file="RunOnGithubActionOnlyAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.GitHub;

namespace TedToolkit.ModularPipelines.Attributes;

/// <summary>
/// Only run on GitHub Action.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RunOnGithubActionOnlyAttribute : MandatoryRunConditionAttribute
{
    /// <inheritdoc />
    public override Task<bool> Condition(IPipelineHookContext pipelineContext)
    {
        ArgumentNullException.ThrowIfNull(pipelineContext);
        return Task.FromResult(pipelineContext.ServiceProvider.GetRequiredService<IGitHubEnvironmentVariables>().CI);
    }
}