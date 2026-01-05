// -----------------------------------------------------------------------
// <copyright file="NuGetPipelineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Options;

/// <summary>
/// Nuget的一些设置。.
/// </summary>
public sealed record NuGetPipelineOptions
{
    /// <summary>
    /// Gets 你的Key信息。.
    /// </summary>
    [SecretValue]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets 数据源.
    /// </summary>
#pragma warning disable CA1056
    public required string Url { get; init; }
#pragma warning restore CA1056

    /// <summary>
    /// Gets 推送源.
    /// </summary>
    public string Source
        => new(Url + "v3/index.json");
}