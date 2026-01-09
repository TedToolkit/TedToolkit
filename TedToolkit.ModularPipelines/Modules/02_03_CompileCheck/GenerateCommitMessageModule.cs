// -----------------------------------------------------------------------
// <copyright file="GenerateCommitMessageModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.Logging;
using ModularPipelines.Models;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Options;

using TextCopy;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Generate the commit message.
/// </summary>
/// <param name="chatClient">chat client.</param>
/// <param name="options">options.</param>
[CanRunAi]
[RunLocalOnly]
public sealed partial class GenerateCommitMessageModule(IChatClient chatClient, IOptions<AiPipelineOptions> options)
    : CompileCheckModule<string>
{
    /// <inheritdoc />
    protected override Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        return Task.FromResult(options.Value.GenerateCommit
            ? SkipDecision.DoNotSkip
            : SkipDecision.Skip("Do not generate commit message by the options."));
    }

    /// <inheritdoc />
    protected override async Task<string?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var commitChanges = await context.GitDiffAsync(
            new(),
            cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(commitChanges))
            return null;

        var aiResult = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, await SharedHelpers.GetCommitMessagePromptAsync(cancellationToken).ConfigureAwait(false)),
                new(ChatRole.User, commitChanges),
            ],
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var resultMessage = aiResult.Messages[0].Text;
        LogResultMessage(context.Logger, resultMessage);
        await ClipboardService.SetTextAsync(resultMessage, cancellationToken).ConfigureAwait(false);
        return resultMessage;
    }

    [LoggerMessage(LogLevel.Error, Message = "{Result}")]
    private static partial void LogResultMessage(IModuleLogger logger, string result);
}