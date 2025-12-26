// -----------------------------------------------------------------------
// <copyright file="SharedHelpers.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Git.Options;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines;

/// <summary>
/// General helpers.
/// </summary>
public static class SharedHelpers
{
    /// <summary>
    /// 获得结果
    /// </summary>
    /// <param name="module">模组</param>
    /// <typeparam name="TModule">模组类型</typeparam>
    /// <typeparam name="TData">模组数据</typeparam>
    /// <returns>返回结果</returns>
    /// <exception cref="InvalidOperationException">非法操作</exception>
    public static async Task<TData> GetResultValueAsync<TModule, TData>(this TModule? module)
        where TModule : Module<TData>
    {
        ArgumentNullException.ThrowIfNull(module);
        var data = await module.GetResult().ConfigureAwait(false);
        if (!data.HasValue)
            throw new InvalidOperationException();

        ArgumentNullException.ThrowIfNull(data.Value);
        return data.Value;
    }

    /// <summary>
    /// Get the editor config file.
    /// </summary>
    /// <param name="cancellationToken">cancel</param>
    /// <returns>string</returns>
    public static Task<string> GetEditorConfigAsync(in CancellationToken cancellationToken)
    {
        return GetManifestResourceStringAsync(
            "TedToolkit.ModularPipelines..editorconfig",
            cancellationToken);
    }

    /// <summary>
    /// Retrieves the commit message prompt from a predefined resource file.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The result contains the commit message prompt as a string.</returns>
    public static Task<string> GetCommitMessagePromptAsync(in CancellationToken cancellationToken)
    {
        return GetManifestResourceStringAsync(
            "TedToolkit.ModularPipelines.CommitMessage.md",
            cancellationToken);
    }

    private static async Task<string> GetManifestResourceStringAsync(string name, CancellationToken cancellationToken)
    {
#pragma warning disable CA2007
        await using var stream = typeof(SharedHelpers).Assembly.GetManifestResourceStream(name)!;
#pragma warning restore CA2007
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获得偏差情况
    /// </summary>
    /// <param name="context">环境</param>
    /// <param name="diffOptions">偏差设计</param>
    /// <param name="cancellationToken">取消</param>
    /// <returns>结果</returns>
    public static async Task<string> GitDiffAsync(
        this IPipelineContext context,
        GitDiffOptions diffOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diffOptions);
        var diffFile = context.GetOutputFolder().GetFile($"diff{Environment.TickCount}.txt");
        await context.Git().Commands.Diff(
                diffOptions with { Output = diffFile.Path, },
                cancellationToken)
            .ConfigureAwait(false);

        var result = await diffFile.ReadAsync(cancellationToken).ConfigureAwait(false);
        diffFile.Delete();
        return result;
    }

    /// <summary>
    /// 根目录
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetRootFolder(this IPipelineContext context)
        => context.Git().RootDirectory;

    /// <summary>
    /// Props文件夹
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetPropsFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(PROPS_FOLDER);

    /// <summary>
    /// 输出文件夹.
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetOutputFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(OUTPUT_FOLDER);

    /// <summary>
    /// 失败的文件.
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static global::ModularPipelines.FileSystem.File GetFailedFile(
        this IPipelineContext context)
    {
        return context.GetOutputFolder().GetFile(FAILED_PROJECTS);
    }

    /// <summary>
    /// 版本文件
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static global::ModularPipelines.FileSystem.File GetVersionFile(this IPipelineContext context)
        => context.GetOutputFolder().GetFile("Version.txt");

    /// <summary>
    /// 获得外部文件夹
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetExternalsFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(EXTERNALS_FOLDER);

    /// <summary>
    /// 运行时Nuget输出文件夹.
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetNugetFolder(this IPipelineContext context)
        => context.GetOutputFolder().CreateFolder(NUGET_FOLDER);

    /// <summary>
    /// 运行时Nuget输出文件夹.
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetTestFolder(this IPipelineContext context)
        => context.GetOutputFolder().CreateFolder(TEST_FOLDER);

    /// <summary>
    /// 运行时Publish输出文件夹.
    /// </summary>
    /// <param name="context">环境</param>
    /// <returns>结果</returns>
    public static Folder GetPublishFolder(this IPipelineContext context)
        => context.GetOutputFolder().CreateFolder(PUBLISH_FOLDER);

    /// <summary>
    ///     Branches.
    /// </summary>
    public const string MAIN_BRANCH = "main";

    /// <summary>
    ///     Branches.
    /// </summary>
    public const string DEVELOPMENT_BRANCH = "development";

    /// <summary>
    /// 关于一些Props
    /// </summary>
    private const string PROPS_FOLDER = "props";

    /// <summary>
    ///     Folders.
    /// </summary>
    private const string OUTPUT_FOLDER = "output";

    /// <summary>
    /// 发布文件夹
    /// </summary>
    private const string PUBLISH_FOLDER = "publish";

    /// <summary>
    ///     Folders.
    /// </summary>
    private const string NUGET_FOLDER = "nuget";

    /// <summary>
    ///     Folders.
    /// </summary>
    private const string TEST_FOLDER = "test";

    /// <summary>
    ///  Externals.
    /// </summary>
    private const string EXTERNALS_FOLDER = "externals";

    /// <summary>
    ///     Folders.
    /// </summary>
    public const string FAILED_PROJECTS = "failedprojects.txt";
}