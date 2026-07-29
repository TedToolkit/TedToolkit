// -----------------------------------------------------------------------
// <copyright file="TargetExecutionResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents the redacted terminal result of one named non-test target.
/// </summary>
public sealed record TargetExecutionResult
{
    /// <summary>
    /// Gets the logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the configuration supplied to the target command.
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target succeeded.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the artifact-root-relative safe failure-log path when the target failed.
    /// </summary>
    public string? FailureLogPath { get; init; }
}