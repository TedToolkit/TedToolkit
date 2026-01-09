// -----------------------------------------------------------------------
// <copyright file="TedPipeline.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using HarmonyLib;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Git.Extensions;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.Host;

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
    static TedPipeline()
    {
        var type = typeof(PipelineHostBuilder).Assembly.GetType("ModularPipelines.Extensions.TypeExtensions");
        var targetMethod = type?.GetMethod("IsOrInheritsFrom");
        if (targetMethod is null)
            return;

        var harmony = new Harmony("ModularPipelines.TedPipeline");
        harmony.Patch(targetMethod, prefix: new(IsOrInheritsFromPrefix));

#pragma warning disable SA1313, IDE1006
        // ReSharper disable once InconsistentNaming
        static bool IsOrInheritsFromPrefix(out bool __result, Type type, Type otherType)
#pragma warning restore SA1313, IDE1006
        {
            if (type == otherType)
            {
                __result = true;
                return false;
            }

            if (!otherType.IsGenericType)
            {
                __result = type.IsSubclassOf(otherType);
                return false;
            }

            var baseType = type.BaseType;
            while (baseType is not null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == otherType)
                {
                    __result = true;
                    return false;
                }

                baseType = baseType.BaseType;
            }

            __result = false;
            return false;
        }
    }

    /// <summary>
    /// execute.
    /// </summary>
    /// <param name="modifyBuilder">modifers.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task ExecuteAsync(Func<PipelineHostBuilder, PipelineHostBuilder>? modifyBuilder = null)
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
    /// 创建一个无任何Modules的.
    /// </summary>
    /// <returns>PipelineHost的Builder.</returns>
    public PipelineHostBuilder CreateNoModules()
    {
        return PipelineHostBuilder.Create()
            .ConfigureAppConfiguration((_, builder) =>
            {
                if (appSettings.Exists)
                    builder.AddJsonFile(appSettings.FullName);

                builder.AddEnvironmentVariables();
            })
            .SetLogLevel(LogLevel.Warning)
            .ConfigureServices((context, collection) =>
            {
                collection
                    .Configure<DotNetPipelineOptions>(context.Configuration.GetSection("DotNet"))
                    .Configure<NuGetPipelineOptions>(context.Configuration.GetSection("NuGet"))
                    .Configure<AiPipelineOptions>(context.Configuration.GetSection("Ai"))
                    .AddSingleton(files)
                    .AddAi(context)
                    .RegisterGitHubContext()
                    .RegisterDotNetContext()
                    .RegisterGitContext();
            })
            .ConfigurePipelineOptions((_, options) =>
            {
                options.DefaultRetryCount = 3;
                options.PrintLogo = false;
            });
    }
}