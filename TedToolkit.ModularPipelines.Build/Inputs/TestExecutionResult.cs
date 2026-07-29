// -----------------------------------------------------------------------
// <copyright file="TestExecutionResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents aggregated, validated TRX outcomes for one named test target.
/// </summary>
public sealed record TestExecutionResult
{
    /// <summary>
    /// Gets the logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the structurally selected .NET test command.
    /// </summary>
    public required DotNetTestCommand Command { get; init; }

    /// <summary>
    /// Gets the configuration supplied to the test command.
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command and parsed outcomes succeeded.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the total parsed test count.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the passed test count.
    /// </summary>
    public int Passed { get; init; }

    /// <summary>
    /// Gets the failed test count.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Gets the skipped or inconclusive test count.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// Gets the artifact-root-relative paths of parsed TRX files.
    /// </summary>
    public IReadOnlyList<string> ResultPaths { get; init; } = [];
}