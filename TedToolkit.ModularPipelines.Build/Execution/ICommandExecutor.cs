// -----------------------------------------------------------------------
// <copyright file="ICommandExecutor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents the internal child-process boundary used by Build and its tests.
/// </summary>
internal interface ICommandExecutor
{
    /// <summary>
    /// Executes a structured request and terminates the process tree on timeout or cancellation.
    /// </summary>
    /// <param name="request">The validated process request.</param>
    /// <param name="cancellationToken">A token that cancels process execution.</param>
    /// <returns>The captured in-memory result.</returns>
    Task<CommandResult> ExecuteAsync(
        CommandRequest request,
        CancellationToken cancellationToken);
}