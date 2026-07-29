// -----------------------------------------------------------------------
// <copyright file="FormatTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents one explicit root-contained <c>dotnet format</c> target.
/// </summary>
public sealed record FormatTarget
{
    /// <summary>
    /// Gets the solution or project file.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// Gets the tracked-file selection policy.
    /// </summary>
    public FormatScope Scope { get; init; } = FormatScope.HeadTracked;

    /// <summary>
    /// Gets additional structured arguments that do not override package-owned switches.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets the command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}