// -----------------------------------------------------------------------
// <copyright file="OperationDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes how an idempotent remote operation completed.
/// </summary>
public enum OperationDisposition
{
    /// <summary>
    /// A new remote resource was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// An existing remote resource was updated.
    /// </summary>
    Updated = 1,

    /// <summary>
    /// An existing matching remote resource was reused.
    /// </summary>
    Reused = 2,

    /// <summary>
    /// NuGet reported a duplicate under explicit skip policy.
    /// </summary>
    SkippedDuplicate = 3,
}