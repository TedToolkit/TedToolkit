// -----------------------------------------------------------------------
// <copyright file="AssertBuildTestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;
using ModularPipelines.DotNet;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Check the data.
/// </summary>
/// <param name="parser">For parse the trx.</param>
public sealed class AssertBuildTestModule(ITrxParser parser) : ReleaseModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        var failedFile = context.GetFailedFile();
        if (failedFile.Exists
            && (await failedFile
                .ReadLinesAsync(cancellationToken)
                .ConfigureAwait(false)).Length != 0)
        {
            throw new InvalidOperationException("Failed to pass the build!");
        }

        foreach (var file in context.GetTestFolder().GetFiles(f => f.Extension is ".trx"))
        {
            var result = parser.ParseTrxContents(await file.ReadAsync(cancellationToken).ConfigureAwait(false));
            var executed = result.ResultSummary.Counters.Executed;
            var passed = result.ResultSummary.Counters.Passed;
            if (executed == passed)
                continue;

            throw new InvalidOperationException("Failed to pass the test!");
        }

        return false;
    }
}