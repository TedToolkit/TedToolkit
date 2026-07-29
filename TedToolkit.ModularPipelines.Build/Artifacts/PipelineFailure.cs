// -----------------------------------------------------------------------
// <copyright file="PipelineFailure.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents a redacted, machine-readable Build failure.
/// </summary>
public sealed record PipelineFailure
{
    /// <summary>
    /// Gets the stable failure code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the stable stage or target that failed.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Gets the safe failure summary, excluding raw process output and secrets.
    /// </summary>
    public required string Summary { get; init; }
}