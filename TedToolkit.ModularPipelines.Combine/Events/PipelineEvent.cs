// -----------------------------------------------------------------------
// <copyright file="PipelineEvent.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Events;

/// <summary>
/// Represents one immutable notification payload.
/// </summary>
public sealed record PipelineEvent
{
    /// <summary>
    /// Gets the event kind.
    /// </summary>
    public required PipelineEventKind Kind { get; init; }

    /// <summary>
    /// Gets an immutable run snapshot.
    /// </summary>
    public required PipelineRunResult RunResult { get; init; }

    /// <summary>
    /// Gets the related change request.
    /// </summary>
    public ChangeRequest? ChangeRequest { get; init; }

    /// <summary>
    /// Gets the related Release.
    /// </summary>
    public Release? Release { get; init; }

    /// <summary>
    /// Gets the related safe failure.
    /// </summary>
    public PipelineFailure? Failure { get; init; }
}