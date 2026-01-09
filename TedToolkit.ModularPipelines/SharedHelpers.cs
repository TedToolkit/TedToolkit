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
    /// Get result.
    /// </summary>
    /// <param name="module">module.</param>
    /// <typeparam name="TModule">module type.</typeparam>
    /// <typeparam name="TData">the data type.</typeparam>
    /// <returns>result.</returns>
    /// <exception cref="InvalidOperationException">invalid operation.</exception>
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
    /// <param name="cancellationToken">cancel.</param>
    /// <returns>string.</returns>
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
    /// Get the diff.
    /// </summary>
    /// <param name="context">context.</param>
    /// <param name="diffOptions">diff options.</param>
    /// <param name="cancellationToken">cancel.</param>
    /// <returns>result.</returns>
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
    /// Root folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetRootFolder(this IPipelineContext context)
        => context.Git().RootDirectory;

    /// <summary>
    /// Props folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetPropsFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(PROPS_FOLDER);

    /// <summary>
    /// output folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetOutputFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(OUTPUT_FOLDER);

    /// <summary>
    /// failed folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static global::ModularPipelines.FileSystem.File GetFailedFile(
        this IPipelineContext context)
    {
        return context.GetOutputFolder().GetFile(FAILED_PROJECTS);
    }

    /// <summary>
    /// version file.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static global::ModularPipelines.FileSystem.File GetVersionFile(this IPipelineContext context)
        => context.GetOutputFolder().GetFile("Version.txt");

    /// <summary>
    /// externals folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetExternalsFolder(this IPipelineContext context)
        => context.GetRootFolder().CreateFolder(EXTERNALS_FOLDER);

    /// <summary>
    /// nuget folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetNugetFolder(this IPipelineContext context)
        => context.GetOutputFolder().CreateFolder(NUGET_FOLDER);

    /// <summary>
    /// test folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
    public static Folder GetTestFolder(this IPipelineContext context)
        => context.GetOutputFolder().CreateFolder(TEST_FOLDER);

    /// <summary>
    /// publish folder.
    /// </summary>
    /// <param name="context">context.</param>
    /// <returns>result.</returns>
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
    /// Props.
    /// </summary>
    private const string PROPS_FOLDER = "props";

    /// <summary>
    ///     Folders.
    /// </summary>
    private const string OUTPUT_FOLDER = "output";

    /// <summary>
    /// publish folder.
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