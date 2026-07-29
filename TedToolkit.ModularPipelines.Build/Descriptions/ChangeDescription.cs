// -----------------------------------------------------------------------
// <copyright file="ChangeDescription.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Descriptions;

/// <summary>
/// Represents a validated, provider-neutral generated description.
/// </summary>
public sealed record ChangeDescription
{
    /// <summary>
    /// Gets the single-line subject or title.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the optional normalized body.
    /// </summary>
    public string? Body { get; init; }
}