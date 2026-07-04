// -----------------------------------------------------------------------
// <copyright file="NuGetPipelineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Options;

/// <summary>
/// NuGet settings.
/// </summary>
public sealed record NuGetPipelineOptions
{
    /// <summary>
    /// Gets the API key.
    /// </summary>
    [SecretValue]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the package URL.
    /// </summary>
#pragma warning disable CA1056
    public required string Url { get; init; }
#pragma warning restore CA1056

    /// <summary>
    /// Gets the package source.
    /// </summary>
    public required string Source { get; init; }
}