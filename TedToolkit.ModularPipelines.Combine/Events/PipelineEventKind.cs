// -----------------------------------------------------------------------
// <copyright file="PipelineEventKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Events;

/// <summary>
/// Identifies a bounded neutral pipeline event.
/// </summary>
public enum PipelineEventKind
{
    /// <summary>
    /// Build output was validated.
    /// </summary>
    BuildValidated = 0,

    /// <summary>
    /// A change request completed.
    /// </summary>
    ChangeRequestCompleted = 1,

    /// <summary>
    /// Requested package or artifact publication completed.
    /// </summary>
    PublicationCompleted = 2,

    /// <summary>
    /// A Release completed.
    /// </summary>
    ReleaseCompleted = 3,

    /// <summary>
    /// The complete pipeline succeeded.
    /// </summary>
    PipelineCompleted = 4,

    /// <summary>
    /// The complete pipeline failed.
    /// </summary>
    PipelineFailed = 5,
}