// -----------------------------------------------------------------------
// <copyright file="BuildTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents one named root-contained <c>dotnet build</c> target.
/// </summary>
public sealed record BuildTarget
{
    /// <summary>
    /// Gets the unique logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the solution or project file.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// Gets the build configuration.
    /// </summary>
    public string Configuration { get; init; } = "Release";

    /// <summary>
    /// Gets a value indicating whether an exact matching clean gates the build.
    /// </summary>
    public bool CleanBeforeBuild { get; init; }

    /// <summary>
    /// Gets additional structured arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets the per-command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}