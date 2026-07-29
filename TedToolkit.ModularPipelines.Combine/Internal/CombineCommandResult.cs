// -----------------------------------------------------------------------
// <copyright file="CombineCommandResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Describes a safe package-command result.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="TimedOut">Whether the timeout elapsed.</param>
/// <param name="Cancelled">Whether caller cancellation occurred.</param>
/// <param name="Duplicate">Whether a confirmed skipped duplicate was reported.</param>
internal sealed record CombineCommandResult(
    int ExitCode,
    bool TimedOut,
    bool Cancelled,
    bool Duplicate);