// -----------------------------------------------------------------------
// <copyright file="PublishOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Constants;

/// <summary>
/// 发布选项
/// </summary>
/// <param name="File">文件</param>
/// <param name="Framework">目标框架</param>
public readonly record struct PublishOptions(
    FileInfo File,
    string? Framework = null)
{
    /// <summary>
    /// 从File路径中转换过来
    /// </summary>
    /// <param name="file">文件路径</param>
    /// <returns>选项</returns>
#pragma warning disable CA2225
    public static implicit operator PublishOptions(FileInfo file)
#pragma warning restore CA2225
        => new(file);
}