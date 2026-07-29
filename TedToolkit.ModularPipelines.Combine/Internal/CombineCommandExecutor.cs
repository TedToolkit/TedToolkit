// -----------------------------------------------------------------------
// <copyright file="CombineCommandExecutor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Runs a bounded child process without persisting raw output.
/// </summary>
internal sealed class CombineCommandExecutor : ICombineCommandExecutor
{
    /// <inheritdoc/>
    public async Task<CombineCommandResult> ExecuteAsync(
        CombineCommandRequest request,
        string? sensitiveArgument,
        CancellationToken cancellationToken)
    {
        _ = sensitiveArgument;
        using var process = new Process()
        {
            StartInfo = new()
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The package command could not start.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(
            CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(
            CancellationToken.None);
        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            _ = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            return new(
                process.ExitCode,
                timeoutSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                false);
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var duplicate = process.ExitCode == 0
                        && (output.Contains(
                                "already exists",
                                StringComparison.OrdinalIgnoreCase)
                            || error.Contains(
                                "already exists",
                                StringComparison.OrdinalIgnoreCase));
        return new(process.ExitCode, false, false, duplicate);
    }
}