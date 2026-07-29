using System.Diagnostics;

namespace TedToolkit.Build.ReleaseLifecycle;

/// <summary>
/// Executes argument-list-only Git commands for the non-packable repository host.
/// </summary>
internal sealed class GitCommandRunner(DirectoryInfo repositoryRoot)
{
    /// <summary>
    /// Runs Git and captures its standard streams.
    /// </summary>
    /// <param name="arguments">The exact Git arguments.</param>
    /// <param name="standardInput">Optional standard input.</param>
    /// <param name="cancellationToken">A token that cancels the process.</param>
    /// <returns>The completed command result.</returns>
    public async Task<GitCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new("git")
            {
                WorkingDirectory = repositoryRoot.FullName,
                RedirectStandardInput = standardInput is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start Git.");
        }

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(
                    standardInput.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    /// <summary>
    /// Runs Git and requires a successful exit code.
    /// </summary>
    /// <param name="arguments">The exact Git arguments.</param>
    /// <param name="standardInput">Optional standard input.</param>
    /// <param name="cancellationToken">A token that cancels the process.</param>
    /// <returns>Standard output.</returns>
    public async Task<string> RequireAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
                arguments,
                standardInput,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return result.StandardOutput;
        }

        throw new InvalidOperationException(
            $"Git command failed with exit code {result.ExitCode}: "
            + Sanitize(result.StandardError));
    }

    private static string Sanitize(string value)
    {
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 512
            ? normalized
            : normalized[..512];
    }
}

/// <summary>
/// Represents one completed Git command.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
internal sealed record GitCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);