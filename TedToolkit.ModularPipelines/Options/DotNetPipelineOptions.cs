// -----------------------------------------------------------------------
// <copyright file="DotNetPipelineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace TedToolkit.ModularPipelines.Options;

/// <summary>
/// 编译选项.
/// </summary>
public sealed record DotNetPipelineOptions
{
    /// <summary>
    /// Gets 编译选项.
    /// </summary>
    public required string Configuration { get; init; } = "Release";

    /// <summary>
    /// 是否需要Format
    /// </summary>
    public bool Format { get; init; }

    /// <summary>
    /// 跳过更新EditorConfig
    /// </summary>
    public bool SkipUpdateEditorConfig { get; init; }

    /// <summary>
    /// 使用本地导入的方式的Condition的String
    /// </summary>
    [SuppressMessage("Minor Code Smell",
        "S2325:Methods and properties that don't access instance data should be static",
        Justification = "当前这个属性不应为静态")]
    public string LoadLocalConditionString
    {
        get => string.IsNullOrEmpty(field) ? "LoadLocal" : field;
        init;
    }
}