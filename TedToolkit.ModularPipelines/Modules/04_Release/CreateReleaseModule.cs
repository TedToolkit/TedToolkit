// -----------------------------------------------------------------------
// <copyright file="CreateReleaseModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Attributes;
using ModularPipelines.GitHub;

using Octokit;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Create the release on GitHub.
/// </summary>
/// <param name="githubClient">client.</param>
/// <param name="gitHubEnvironmentVariables">variable.</param>
/// <param name="nugetOptions">nuget.</param>
[RunOnGithubActionOnly]
[DependsOn<NugetPushModule>]
[RunIfBranch(SharedHelpers.MAIN_BRANCH)]
public sealed class CreateReleaseModule(
    IGitHub githubClient,
    IGitHubEnvironmentVariables gitHubEnvironmentVariables,
    IOptions<NuGetPipelineOptions> nugetOptions) : ReleaseModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var version = await context.GetVersionFile().ReadAsync(cancellationToken).ConfigureAwait(false);

        var repositoryId = long.Parse(gitHubEnvironmentVariables.RepositoryId!, CultureInfo.InvariantCulture);
        var repository = await githubClient.Client.Repository.Get(repositoryId).ConfigureAwait(false);

        var releaseNote = new StringBuilder();

#pragma warning disable CA1305
        releaseNote
            .AppendLine($"# {version} ({DateTime.UtcNow.Date.ToString("yyyy-M-d dddd")})")
            .AppendLine(repository.Description);

        foreach (var listFolder in context.GetNugetFolder().ListFolders())
        {
            var packageName = listFolder.Name[..^version.Length];
            releaseNote.AppendLine($"- [{packageName}]({nugetOptions.Value.Url}/packages/{packageName})");
        }
#pragma warning restore CA1305

        context.GetNugetFolder().Delete();

        await githubClient.Client.Repository.Release.Create(repositoryId,
            new NewRelease(version)
            {
                Name = version, Body = releaseNote.ToString(), Draft = false, Prerelease = false,
            }).ConfigureAwait(false);

        return true;
    }
}