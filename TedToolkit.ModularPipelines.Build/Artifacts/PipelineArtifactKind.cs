// -----------------------------------------------------------------------
// <copyright file="PipelineArtifactKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents the supported durable artifact classifications in a Build handoff.
/// </summary>
public enum PipelineArtifactKind
{
    /// <summary>
    /// Represents a primary NuGet package.
    /// </summary>
    NuGetPackage = 0,

    /// <summary>
    /// Represents a NuGet symbol package.
    /// </summary>
    SymbolPackage = 1,

    /// <summary>
    /// Represents a deterministic archive produced by <c>dotnet publish</c>.
    /// </summary>
    PublishArchive = 2,

    /// <summary>
    /// Represents a test result or auxiliary test-output file.
    /// </summary>
    TestResult = 3,

    /// <summary>
    /// Represents a package-authored redacted failure record.
    /// </summary>
    FailureLog = 4,
}