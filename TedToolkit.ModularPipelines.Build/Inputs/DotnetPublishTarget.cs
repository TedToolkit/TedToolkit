// -----------------------------------------------------------------------
// <copyright file="DotnetPublishTarget.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents one named local <c>dotnet publish</c> and deterministic archive target.
/// </summary>
public sealed record DotnetPublishTarget
{
    /// <summary>
    /// Gets the unique logical target name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the portable logical artifact name.
    /// </summary>
    public required string ArtifactName { get; init; }

    /// <summary>
    /// Gets the publishable project file.
    /// </summary>
    public required FileInfo File { get; init; }

    /// <summary>
    /// Gets the publish configuration.
    /// </summary>
    public string Configuration { get; init; } = "Release";

    /// <summary>
    /// Gets the optional target framework.
    /// </summary>
    public string? Framework { get; init; }

    /// <summary>
    /// Gets the optional runtime identifier.
    /// </summary>
    public string? RuntimeIdentifier { get; init; }

    /// <summary>
    /// Gets the required explicit self-contained choice when a runtime identifier is supplied.
    /// </summary>
    public bool? SelfContained { get; init; }

    /// <summary>
    /// Gets additional structured arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets the command timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
}