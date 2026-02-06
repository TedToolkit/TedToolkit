// -----------------------------------------------------------------------
// <copyright file="NugetPushModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;

using Microsoft.Extensions.Options;

using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Git.Attributes;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Publish the nuget packages.
/// </summary>
/// <param name="nugetOptions">publish options.</param>
[RunOnGithubActionOnly]
[RunIfBranch(SharedHelpers.MAIN_BRANCH)]
public sealed class NugetPushModule(IOptions<NuGetPipelineOptions> nugetOptions) : ReleaseModule<bool>
{
    /// <inheritdoc />
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration.Create()
            .WithSkipWhen(context =>
            {
                if (context.Git().Information.BranchName is not SharedHelpers.MAIN_BRANCH)
                {
                    return SkipDecision.Skip(
                        $"No need to push nuget packages on non-{SharedHelpers.MAIN_BRANCH}");
                }

                return SkipDecision.DoNotSkip;
            })
            .Build();
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(context.GetNugetFolder().ListFolders().Select(async folder =>
        {
            var fullPath = folder.Path + ".nupkg";
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            await ZipFile.CreateFromDirectoryAsync(folder.Path, fullPath, cancellationToken).ConfigureAwait(false);

            await context.DotNet().Nuget.Push(
                new()
                {
                    Path = fullPath,
                    Source = nugetOptions.Value.Source,
                    ApiKey = nugetOptions.Value.ApiKey,
                    SkipDuplicate = true,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        })).ConfigureAwait(false);

        return true;
    }
}