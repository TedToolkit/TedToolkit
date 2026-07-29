// -----------------------------------------------------------------------
// <copyright file="BuildOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Resources;

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents optional Build behavior, conventions, resources, and bounded parallelism.
/// </summary>
public sealed record BuildOptions
{
    /// <summary>
    /// Gets branch, remote, and layout conventions.
    /// </summary>
    public PipelineConventionOptions Conventions { get; init; } = new();

    /// <summary>
    /// Gets embedded resource composition and write options.
    /// </summary>
    public EmbeddedResourceOptions Resources { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether LocalBuild runs formatting.
    /// </summary>
    public bool RunFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether eligible profiles may call an optional generator.
    /// </summary>
    public bool GenerateChangeDescriptions { get; init; }

    /// <summary>
    /// Gets the positive maximum parallel target count.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } =
        Math.Max(1, Math.Min(Environment.ProcessorCount, 4));

    /// <summary>
    /// Gets how enabled description-generator failures affect Build.
    /// </summary>
    public ChangeDescriptionFailureMode ChangeDescriptionFailureMode { get; init; } =
        ChangeDescriptionFailureMode.Continue;
}