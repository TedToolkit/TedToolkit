// -----------------------------------------------------------------------
// <copyright file="PublicationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains explicit publication identity and Release content.
/// </summary>
public sealed record PublicationOptions
{
    /// <summary>
    /// Gets the normalized publication version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the explicit repository tag.
    /// </summary>
    public string? TagName { get; init; }

    /// <summary>
    /// Gets the Release title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the Release body.
    /// </summary>
    public string Body { get; init; } = "";
}