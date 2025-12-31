// -----------------------------------------------------------------------
// <copyright file="CreatePullRequestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using ModularPipelines.Context;
using ModularPipelines.GitHub;
using ModularPipelines.Models;

using Octokit;

using TedToolkit.ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Create a pr.
/// </summary>
/// <param name="githubClient">mr的client</param>
/// <param name="gitHubEnvironmentVariables">environments.</param>
[RunOnGithubActionOnly]
public sealed class CreatePullRequestModule(IGitHub githubClient, IGitHubEnvironmentVariables gitHubEnvironmentVariables)
    : ReleaseModule<PullRequest>
{
    private string SourceBranch
        => gitHubEnvironmentVariables.RefName!;

    private string TargetBranch
        => RemoveSourceBranch ? SharedHelpers.DEVELOPMENT_BRANCH : SharedHelpers.MAIN_BRANCH;

    private string Title
        => RemoveSourceBranch ? SourceBranch : "🔖 Release";

    private bool RemoveSourceBranch
        => SourceBranch is not SharedHelpers.DEVELOPMENT_BRANCH;

    /// <inheritdoc/>
    protected override async Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        if (SourceBranch is SharedHelpers.MAIN_BRANCH)
        {
            return SkipDecision.Skip(
                $"No need to create a PR on {SharedHelpers.MAIN_BRANCH}");
        }

        var prs = await githubClient.Client.PullRequest.GetAllForRepository(long.Parse(
                    gitHubEnvironmentVariables.RepositoryId!,
                    CultureInfo.CurrentCulture),
                new PullRequestRequest() { Base = SourceBranch, Head = TargetBranch, State = ItemStateFilter.Open, })
            .ConfigureAwait(false);

        if (prs.Count > 0)
        {
            return SkipDecision.Skip(
                $"There is an PR that merge from {SourceBranch} to {TargetBranch}");
        }

        return SkipDecision.DoNotSkip;
    }

    /// <inheritdoc />
#pragma warning disable AsyncModule
    protected override Task<PullRequest?> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        return githubClient.Client.PullRequest.Create(long.Parse(
                gitHubEnvironmentVariables.RepositoryId!,
                CultureInfo.CurrentCulture),
            new NewPullRequest(Title, TargetBranch, SourceBranch) { Draft = true, });
    }
#pragma warning restore AsyncModule
}