// -----------------------------------------------------------------------
// <copyright file="PipelineTriggerKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Conventions;

/// <summary>
/// Represents provider-neutral pipeline trigger categories.
/// </summary>
public enum PipelineTriggerKind
{
    /// <summary>
    /// Indicates a local invocation.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Indicates a branch push.
    /// </summary>
    Push = 1,

    /// <summary>
    /// Indicates an explicitly dispatched invocation.
    /// </summary>
    Manual = 2,

    /// <summary>
    /// Indicates a reusable workflow invocation.
    /// </summary>
    ReusableCall = 3,

    /// <summary>
    /// Indicates a tag invocation.
    /// </summary>
    Other = 4,
}