// -----------------------------------------------------------------------
// <copyright file="IReleaseFinalizer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Execution;

/// <summary>
/// Performs an explicit provider-neutral Release-only recovery.
/// </summary>
public interface IReleaseFinalizer
{
    /// <summary>
    /// Creates or reuses the requested Release without another pipeline action.
    /// </summary>
    /// <param name="options">The explicit provider and Release request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The verified Release result.</returns>
    Task<ReleasePublicationResult> FinalizeAsync(
        ReleaseFinalizationOptions options,
        CancellationToken cancellationToken);
}