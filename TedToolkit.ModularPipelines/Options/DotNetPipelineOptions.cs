// -----------------------------------------------------------------------
// <copyright file="DotNetPipelineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace TedToolkit.ModularPipelines.Options;

/// <summary>
/// Build options.
/// </summary>
public sealed record DotNetPipelineOptions
{
    /// <summary>
    /// Gets configurations.
    /// </summary>
    public required string Configuration { get; init; } = "Release";

    /// <summary>
    /// Gets a value indicating whether should format.
    /// </summary>
    public bool Format { get; init; }

    /// <summary>
    /// Gets a value indicating whether skip the editor config replace.
    /// </summary>
    public bool SkipUpdateEditorConfig { get; init; }

    /// <summary>
    /// Gets the condition string.
    /// </summary>
    [SuppressMessage("Minor Code Smell",
        "S2325:Methods and properties that don't access instance data should be static",
        Justification = "当前这个属性不应为静态")]
    public string LoadLocalConditionString
    {
        get => string.IsNullOrEmpty(field) ? "LoadLocal" : field;
        init;
    }
}