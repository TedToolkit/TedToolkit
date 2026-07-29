// -----------------------------------------------------------------------
// <copyright file="EmbeddedResourceOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents independent root-contained overlays, replacements, and editor write authorization.
/// </summary>
public sealed record EmbeddedResourceOptions
{
    /// <summary>
    /// Gets the optional editor overlay path.
    /// </summary>
    public string? EditorConfigOverlayPath { get; init; }

    /// <summary>
    /// Gets the optional editor replacement path.
    /// </summary>
    public string? EditorConfigReplacementPath { get; init; }

    /// <summary>
    /// Gets the optional commit-instruction overlay path.
    /// </summary>
    public string? CommitMessageOverlayPath { get; init; }

    /// <summary>
    /// Gets the optional commit-instruction replacement path.
    /// </summary>
    public string? CommitMessageReplacementPath { get; init; }

    /// <summary>
    /// Gets the optional change-request overlay path.
    /// </summary>
    public string? ChangeRequestOverlayPath { get; init; }

    /// <summary>
    /// Gets the optional change-request replacement path.
    /// </summary>
    public string? ChangeRequestReplacementPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the composed editor document may be written.
    /// </summary>
    public bool WriteEditorConfig { get; init; }
}