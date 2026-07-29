// -----------------------------------------------------------------------
// <copyright file="NotificationFailureMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects how notification failures affect the pipeline.
/// </summary>
public enum NotificationFailureMode
{
    /// <summary>
    /// Records a warning and continues.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Fails before subsequent nonterminal work.
    /// </summary>
    FailPipeline = 1,
}