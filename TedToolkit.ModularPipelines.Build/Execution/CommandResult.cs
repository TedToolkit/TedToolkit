// -----------------------------------------------------------------------
// <copyright file="CommandResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents the in-memory result of one child process.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Captured standard output, which is never persisted by Build.</param>
/// <param name="StandardError">Captured standard error, which is never persisted by Build.</param>
/// <param name="TimedOut">Whether the timeout cancelled the process.</param>
/// <param name="Cancelled">Whether caller cancellation cancelled the process.</param>
internal sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    bool Cancelled);