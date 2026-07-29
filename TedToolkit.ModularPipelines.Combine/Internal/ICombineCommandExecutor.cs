// -----------------------------------------------------------------------
// <copyright file="ICombineCommandExecutor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Defines the internal structured child-process boundary.
/// </summary>
internal interface ICombineCommandExecutor
{
    /// <summary>
    /// Runs one bounded command.
    /// </summary>
    /// <param name="request">The structured command.</param>
    /// <param name="sensitiveArgument">The value that must never be persisted.</param>
    /// <param name="cancellationToken">A token that cancels execution.</param>
    /// <returns>The safe command result.</returns>
    Task<CombineCommandResult> ExecuteAsync(
        CombineCommandRequest request,
        string? sensitiveArgument,
        CancellationToken cancellationToken);
}