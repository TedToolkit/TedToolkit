// -----------------------------------------------------------------------
// <copyright file="TedPipeline.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModularPipelines;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Extensions;
using ModularPipelines.Git.Extensions;
using ModularPipelines.GitHub.Extensions;

using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Modules;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines;

/// <summary>
/// Basic pipelines.
/// </summary>
/// <param name="files">FileInfo.</param>
/// <param name="appSettings">Settings.</param>
public class TedPipeline(PipelineFiles files, FileInfo appSettings)
{
    /// <summary>
    /// execute.
    /// </summary>
    /// <param name="modifyBuilder">modifiers.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task ExecuteAsync(Func<PipelineBuilder, PipelineBuilder>? modifyBuilder = null)
    {
        var builder = CreateNoModules()
            .AddModule<GenerateCommitMessageModule>()
            .AddModule<AssertBuildTestModule>()
            .AddModule<FormatAllCodeModule>()
            .AddModule<PublishModule>()
            .AddModule<BumpVersionModule>()
            .AddModule<CleanOutputModule>()
            .AddModule<TestModule>()
            .AddModule<UpdateEditorConfigModule>()
            .AddModule<CreatePullRequestModule>()
            .AddModule<ModifyBranchMergeRequestModule>()
            .AddModule<NugetPushModule>()
            .AddModule<CreateReleaseModule>()
            .AddModule<DotnetBuildModule>();

        builder = modifyBuilder?.Invoke(builder) ?? builder;
        return builder.ExecutePipelineAsync();
    }

    /// <summary>
    /// Creates a builder without any modules.
    /// </summary>
    /// <returns>The pipeline builder.</returns>
    public PipelineBuilder CreateNoModules()
    {
        var builder = Pipeline.CreateBuilder();
        if (appSettings.Exists)
        {
            builder.Configuration.AddJsonFile(appSettings.FullName);
        }

        builder.Configuration.AddEnvironmentVariables();
        return builder
            .SetLogLevel(LogLevel.Warning)
            .ConfigureServices((context, collection) =>
            {
                collection
                    .Configure<DotNetPipelineOptions>(context.Configuration.GetSection("DotNet"))
                    .Configure<NuGetPipelineOptions>(context.Configuration.GetSection("NuGet"))
                    .AddSingleton(files)
                    .AddAi(context);
            })
            .ConfigurePipelineOptions((_, options) =>
            {
                options.DefaultRetryCount = 3;
                options.PrintLogo = false;
                options.DefaultHttpTimeout = TimeSpan.FromMinutes(10);
            });
    }
}