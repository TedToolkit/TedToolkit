// -----------------------------------------------------------------------
// <copyright file="AiPipelineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Options;

/// <summary>
/// The options for AI, I use Gemini for the AI integration.
/// </summary>
public sealed record AiPipelineOptions
{
    /// <summary>
    /// Gets api.
    /// </summary>
    [SecretValue]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets end Point.
    /// </summary>
    public string EndPoint { get; init; } = "";

    /// <summary>
    /// Gets model.
    /// </summary>
    public string ModelId { get; init; } = "";

    /// <summary>
    /// Gets a value indicating whether generate Commit.
    /// </summary>
    public bool GenerateCommit { get; init; } = true;
}