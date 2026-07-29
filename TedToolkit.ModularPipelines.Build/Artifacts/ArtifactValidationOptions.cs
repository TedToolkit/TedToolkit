// -----------------------------------------------------------------------
// <copyright file="ArtifactValidationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents caller-controlled manifest source-revision validation.
/// </summary>
public sealed record ArtifactValidationOptions
{
    /// <summary>
    /// Gets the source revision expected by the consumer, when one is available.
    /// </summary>
    public string? ExpectedSourceRevision { get; init; }

    /// <summary>
    /// Gets a value indicating whether an explicit source-revision mismatch is accepted.
    /// </summary>
    public bool AllowSourceRevisionMismatch { get; init; }
}