// -----------------------------------------------------------------------
// <copyright file="PackTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents one named explicit <c>dotnet pack</c> target.
/// </summary>
public sealed record PackTarget
{
    /// <summary>
    /// Gets the unique logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the packable project file.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// Gets the normalized package version.
    /// </summary>
    public required string PackageVersion { get; init; }

    /// <summary>
    /// Gets the pack configuration.
    /// </summary>
    public string Configuration { get; init; } = "Release";

    /// <summary>
    /// Gets additional structured arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets the command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}