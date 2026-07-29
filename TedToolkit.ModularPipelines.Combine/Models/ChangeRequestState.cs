// -----------------------------------------------------------------------
// <copyright file="ChangeRequestState.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Identifies the neutral change-request state.
/// </summary>
public enum ChangeRequestState
{
    /// <summary>
    /// The request is open.
    /// </summary>
    Open = 0,

    /// <summary>
    /// The request is closed.
    /// </summary>
    Closed = 1,

    /// <summary>
    /// The request is merged.
    /// </summary>
    Merged = 2,
}