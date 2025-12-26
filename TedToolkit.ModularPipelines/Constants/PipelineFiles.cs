// -----------------------------------------------------------------------
// <copyright file="PipelineFiles.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Constants;

/// <summary>
/// 用于处理的一些 Pipelines.
/// </summary>
public sealed record PipelineFiles
{
    /// <summary>
    ///  编译用文件们.
    /// </summary>
    public required IReadOnlyCollection<FileInfo> BuildFiles { get; init; }

    /// <summary>
    ///  测试用文件们.
    /// </summary>
    public required IReadOnlyCollection<FileInfo> TestFiles { get; init; }

    /// <summary>
    ///  发布文件们
    /// </summary>
    public IReadOnlyCollection<PublishOptions> PublishFiles { get; init; } = [];

    /// <summary>
    /// 解决方案
    /// </summary>
    public required FileInfo Solution { get; init; }
}