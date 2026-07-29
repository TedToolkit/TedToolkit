// -----------------------------------------------------------------------
// <copyright file="TestTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents one named test target with an explicit runner command shape.
/// </summary>
public sealed record TestTarget
{
    /// <summary>
    /// Gets the unique logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the test project file.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// Gets the selected command shape.
    /// </summary>
    public DotNetTestCommand Command { get; init; }

    /// <summary>
    /// Gets the test configuration.
    /// </summary>
    public string Configuration { get; init; } = "Release";

    /// <summary>
    /// Gets structured arguments placed before the runner separator.
    /// </summary>
    public IReadOnlyList<string> CommandArguments { get; init; } = [];

    /// <summary>
    /// Gets structured runner arguments placed after the package-owned separator.
    /// </summary>
    public IReadOnlyList<string> RunnerArguments { get; init; } = [];

    /// <summary>
    /// Gets the command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}