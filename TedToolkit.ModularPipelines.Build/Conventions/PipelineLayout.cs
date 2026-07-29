// -----------------------------------------------------------------------
// <copyright file="PipelineLayout.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Conventions;

/// <summary>
/// Represents portable single-segment names used by the standard pipeline layout.
/// </summary>
public sealed record PipelineLayout
{
    /// <summary>
    /// Gets the shared props directory name.
    /// </summary>
    public string PropsDirectoryName { get; init; } = "props";

    /// <summary>
    /// Gets the artifact output directory name.
    /// </summary>
    public string OutputDirectoryName { get; init; } = "output";

    /// <summary>
    /// Gets the external-input directory name.
    /// </summary>
    public string ExternalsDirectoryName { get; init; } = "externals";

    /// <summary>
    /// Gets the NuGet artifact directory name.
    /// </summary>
    public string NuGetDirectoryName { get; init; } = "nuget";

    /// <summary>
    /// Gets the test artifact directory name.
    /// </summary>
    public string TestDirectoryName { get; init; } = "test";

    /// <summary>
    /// Gets the publish archive directory name.
    /// </summary>
    public string PublishDirectoryName { get; init; } = "publish";

    /// <summary>
    /// Gets the root-level manifest filename.
    /// </summary>
    public string ManifestFileName { get; init; } = "pipeline-artifacts.v1.json";
}