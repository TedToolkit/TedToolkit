// -----------------------------------------------------------------------
// <copyright file="NugetPushModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Git.Attributes;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Publish the nuget packages.
/// </summary>
/// <param name="nugetOptions">publish options</param>
[RunOnGithubActionOnly]
[RunIfBranch(SharedHelpers.MAIN_BRANCH)]
public sealed class NugetPushModule(IOptions<NuGetPipelineOptions> nugetOptions) : ReleaseModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(context.GetNugetFolder().ListFolders().Select(async folder =>
        {
            var fullPath = folder.Path + ".nupkg";
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            ZipFile.CreateFromDirectory(folder.Path, fullPath);

            await context.DotNet().Nuget.Push(
                new(fullPath)
                {
                    Source = nugetOptions.Value.Source.AbsolutePath,
                    ApiKey = nugetOptions.Value.ApiKey,
                    SkipDuplicate = true,
                },
                cancellationToken).ConfigureAwait(false);
        })).ConfigureAwait(false);

        context.GetNugetFolder().Delete();
        return true;
    }
}