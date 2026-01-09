// -----------------------------------------------------------------------
// <copyright file="FormatAllCodeModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Format the codes.
/// </summary>
/// <param name="files">files.</param>
/// <param name="dotnet">dotnet options.</param>
[RunLocalOnly]
public sealed class FormatAllCodeModule(PipelineFiles files, IOptions<DotNetPipelineOptions> dotnet)
    : PrepareModule<bool>, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(16);

    /// <inheritdoc />
    protected override TimeSpan Timeout
        => TimeSpan.FromHours(30);

    /// <inheritdoc/>
    protected override Task<SkipDecision> ShouldSkip(IPipelineContext context)
        => Task.FromResult(dotnet.Value.Format ? SkipDecision.DoNotSkip : SkipDecision.Skip("Do not format."));

    /// <inheritdoc/>
    protected override async Task<bool> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        var diffResult = await context.Git().Commands.Diff(
                new() { NameOnly = true, },
                cancellationToken)
            .ConfigureAwait(false);

        await Task.WhenAll(diffResult.StandardOutput.Split('\n')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(async includeFile =>
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    try
                    {
                        await SubModule(
                                Path.GetFileName(includeFile),
                                () => context.DotNet().Format(
                                    new(files.Solution.FullName) { Include = includeFile, },
                                    cancellationToken))
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }))
            .ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public void Dispose()
        => _semaphore.Dispose();
}