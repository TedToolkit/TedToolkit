// -----------------------------------------------------------------------
// <copyright file="PipelineFiles.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Constants;

/// <summary>
/// Pipeline files.
/// </summary>
public sealed record PipelineFiles
{
    /// <summary>
    ///  Gets build files.
    /// </summary>
    public required IReadOnlyCollection<FileInfo> BuildFiles { get; init; }

    /// <summary>
    ///  Gets test files.
    /// </summary>
    public required IReadOnlyCollection<FileInfo> TestFiles { get; init; }

    /// <summary>
    ///  Gets files to publish.
    /// </summary>
    public IReadOnlyCollection<PublishOptions> PublishFiles { get; init; } = [];

    /// <summary>
    /// Gets the Solution.
    /// </summary>
    public required FileInfo Solution { get; init; }
}