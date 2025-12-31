// -----------------------------------------------------------------------
// <copyright file="ModifyBranchMergeRequestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;

using Microsoft.Extensions.AI;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.GitHub;
using ModularPipelines.Models;
using ModularPipelines.Modules;

using Octokit;

using TedToolkit.ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Update the PR.
/// </summary>
/// <param name="githubClient">GitHub client</param>
/// <param name="gitHubEnvironmentVariables">environment</param>
/// <param name="chatClient">chat message</param>
[DependsOn<CreatePullRequestModule>]
[CanRunAi]
[RunOnGithubActionOnly]
public sealed class ModifyBranchMergeRequestModule(
    IGitHub githubClient,
    IGitHubEnvironmentVariables gitHubEnvironmentVariables,
    IChatClient chatClient)
    : ReleaseModule<PullRequest>
{
    private PullRequest? _pullRequest;

    private string TargetBranch
    {
        get
        {
            return gitHubEnvironmentVariables.RefName is SharedHelpers.DEVELOPMENT_BRANCH
                ? SharedHelpers.MAIN_BRANCH
                : SharedHelpers.DEVELOPMENT_BRANCH;
        }
    }

    /// <inheritdoc />
    protected override async Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        var prs = await githubClient.Client.PullRequest.GetAllForRepository(long.Parse(
                    gitHubEnvironmentVariables.RepositoryId!,
                    CultureInfo.CurrentCulture),
                new PullRequestRequest() { Head = TargetBranch, State = ItemStateFilter.Open, })
            .ConfigureAwait(false);

        _pullRequest = prs?.Count > 0 ? prs[0] : null;

        if (_pullRequest is null)
        {
            return SkipDecision.Skip(
                $"Can't find PR from {gitHubEnvironmentVariables.RefName} to {TargetBranch}");
        }

        return SkipDecision.DoNotSkip;
    }

    /// <inheritdoc />
    protected override async Task<PullRequest?> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        if (_pullRequest is null)
            return null;

        if (TargetBranch is SharedHelpers.MAIN_BRANCH)
        {
            var gitVersioningInformation = await context.Git().Commands.Log(
                    new()
                    {
                        Arguments =
                        [
                            $"origin/{SharedHelpers.MAIN_BRANCH}..origin/{gitHubEnvironmentVariables.RefName}",
                        ],
                        Pretty = "format:\"%H|%s\"",
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var lines = gitVersioningInformation.StandardOutput.Split('\n').Select(l =>
            {
                var lines = l.Split('|');
                return $"- {lines[1]} {lines[0]}";
            });

            return await githubClient.Client.PullRequest.Update(long.Parse(
                    gitHubEnvironmentVariables.RepositoryId!,
                    CultureInfo.CurrentCulture),
                _pullRequest.Number,
                new PullRequestUpdate() { Body = string.Join('\n', lines), }).ConfigureAwait(false);
        }

        var diffMessage = await context.GitDiffAsync(
                new() { Arguments = [$"origin/{TargetBranch}..origin/{gitHubEnvironmentVariables.RefName}",], },
                cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(diffMessage))
            return null;

        var aiResult = await chatClient.GetResponseAsync(
                [
                    new(ChatRole.System,
                        await SharedHelpers.GetCommitMessagePromptAsync(cancellationToken).ConfigureAwait(false)),
                    new(ChatRole.User, diffMessage),
                ],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var resultMessages = aiResult.Messages[0].Text.Split('\n');

        var title = resultMessages[0];
        var description = new StringBuilder(string.Join('\n', resultMessages.Skip(1).SkipWhile(string.IsNullOrEmpty)));

        var firstCloses = true;
        if (!string.IsNullOrEmpty(_pullRequest.Body))
        {
            foreach (var se in _pullRequest.Body.Split('\n'))
            {
                var str = se.Trim();
                if (!str.StartsWith("Closes", StringComparison.CurrentCulture)
                    && !str.StartsWith("Fixes", StringComparison.CurrentCulture))
                {
                    continue;
                }

                if (firstCloses)
                {
                    description.Append('\n');
                    firstCloses = false;
                }

                description
                    .Append('\n')
                    .Append(str);
            }
        }

        return await githubClient.Client.PullRequest.Update(long.Parse(
                gitHubEnvironmentVariables.RepositoryId!,
                CultureInfo.CurrentCulture),
            _pullRequest.Number,
            new PullRequestUpdate() { Title = title, Body = description.ToString(), }).ConfigureAwait(false);
    }
}