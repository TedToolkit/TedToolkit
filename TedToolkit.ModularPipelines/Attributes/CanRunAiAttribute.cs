// -----------------------------------------------------------------------
// <copyright file="CanRunAiAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Attributes;

/// <summary>
/// Can run the AI.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CanRunAiAttribute : MandatoryRunConditionAttribute
{
    /// <inheritdoc />
    public override Task<bool> Condition(IPipelineHookContext pipelineContext)
    {
        ArgumentNullException.ThrowIfNull(pipelineContext);
        var options = pipelineContext.Get<IOptions<AiPipelineOptions>>();
        return Task.FromResult(!string.IsNullOrEmpty(options?.Value.ApiKey));
    }
}