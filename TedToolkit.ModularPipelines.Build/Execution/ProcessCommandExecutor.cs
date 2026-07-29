// -----------------------------------------------------------------------
// <copyright file="ProcessCommandExecutor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents the shell-free operating-system process implementation.
/// </summary>
internal sealed class ProcessCommandExecutor : ICommandExecutor
{
    /// <inheritdoc/>
    public async Task<CommandResult> ExecuteAsync(
        CommandRequest request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process() { StartInfo = startInfo, };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Unable to start command '{request.FileName}'.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var timedOut = false;
        var cancelled = false;

        try
        {
            await process.WaitForExitAsync(linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutSource.IsCancellationRequested
                       && !cancellationToken.IsCancellationRequested;
            cancelled = cancellationToken.IsCancellationRequested;

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between cancellation observation and tree termination.
            }

            await process.WaitForExitAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }

        return new(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false),
            timedOut,
            cancelled);
    }
}