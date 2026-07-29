// -----------------------------------------------------------------------
// <copyright file="IPipelineEventSink.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Events;

/// <summary>
/// Receives explicitly enabled provider-neutral pipeline events.
/// </summary>
public interface IPipelineEventSink
{
    /// <summary>
    /// Publishes one immutable event.
    /// </summary>
    /// <param name="pipelineEvent">The event payload.</param>
    /// <param name="cancellationToken">A token that cancels delivery.</param>
    /// <returns>A task that completes after delivery.</returns>
    Task PublishAsync(
        PipelineEvent pipelineEvent,
        CancellationToken cancellationToken);
}