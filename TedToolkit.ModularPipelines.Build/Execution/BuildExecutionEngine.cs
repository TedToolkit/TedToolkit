// -----------------------------------------------------------------------
// <copyright file="BuildExecutionEngine.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Internal;
using TedToolkit.ModularPipelines.Build.Resources;
using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents the internal failure-safe local Build execution graph.
/// </summary>
/// <param name="profile">The selected Build profile.</param>
/// <param name="inputs">The validated Build inputs.</param>
/// <param name="options">The resolved Build options.</param>
/// <param name="commandExecutor">The structured command boundary.</param>
/// <param name="resourceComposer">The resource-composition service.</param>
/// <param name="manifestWriter">The final manifest writer.</param>
/// <param name="descriptionGenerator">The optional consumer generator.</param>
internal sealed class BuildExecutionEngine(
    BuildExecutionProfile profile,
    BuildInputs inputs,
    BuildOptions options,
    ICommandExecutor commandExecutor,
    IPipelineResourceComposer resourceComposer,
    IPipelineArtifactManifestWriter manifestWriter,
    IChangeDescriptionGenerator? descriptionGenerator = null)
{
    private readonly List<PipelineArtifact> _artifacts = [];

    private readonly List<PipelineFailure> _failures = [];

    private readonly List<TargetExecutionResult> _targets = [];

    private readonly List<TestExecutionResult> _tests = [];

    private readonly Dictionary<string, string> _failureLogNames =
        new(StringComparer.OrdinalIgnoreCase);

    private TemporaryMsBuildVersionTransaction? _versionTransaction;

    private ChangeDescription? _generatedDescription;

    private bool _artifactsCollected;

    private bool _initializationCompleted;

    /// <summary>
    /// Gets a value indicating whether no failure has been recorded.
    /// </summary>
    internal bool CanContinue
    {
        get
        {
            return _failures.Count == 0;
        }
    }

    /// <summary>
    /// Gets the execution profile selected for this graph.
    /// </summary>
    internal BuildExecutionProfile Profile
    {
        get
        {
            return profile;
        }
    }

    /// <summary>
    /// Executes the local graph and guarantees restoration and final-manifest attempts.
    /// </summary>
    /// <param name="cancellationToken">A token that cooperatively cancels producer work.</param>
    /// <returns>The immutable Build result.</returns>
    public async Task<BuildResult> RunAsync(
        CancellationToken cancellationToken)
    {
        if (profile == BuildExecutionProfile.None)
        {
            return CreateResult();
        }

        BuildInputValidator.Validate(profile, inputs, options);
        var cleanupToken = CancellationToken.None;

        try
        {
            await RecoverAndCleanAsync(cancellationToken)
                .ConfigureAwait(false);

            await PrepareResourcesAsync(cancellationToken)
                .ConfigureAwait(false);

            if (options.RunFormat
                && !await RunFormatAsync(cancellationToken).ConfigureAwait(false))
            {
                return await FinishAsync(cleanupToken).ConfigureAwait(false);
            }

            if (!await RunBuildTargetsAsync(cancellationToken).ConfigureAwait(false)
                || !await RunTestTargetsAsync(cancellationToken).ConfigureAwait(false))
            {
                return await FinishAsync(cleanupToken).ConfigureAwait(false);
            }

            if (!await GenerateDescriptionAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                return await FinishAsync(cleanupToken).ConfigureAwait(false);
            }

            if (profile is BuildExecutionProfile.Pack
                or BuildExecutionProfile.Publish && !await RunPackTargetsAsync(cancellationToken)
                    .ConfigureAwait(false))
            {
                return await FinishAsync(cleanupToken).ConfigureAwait(false);
            }

            if (profile == BuildExecutionProfile.Publish
                && !await RunPublishTargetsAsync(cancellationToken)
                    .ConfigureAwait(false))
            {
                return await FinishAsync(cleanupToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _failures.Add(new PipelineFailure()
            {
                Code = "BUILD_CANCELLED",
                Stage = "Pipeline",
                Summary = "The Build pipeline was cancelled.",
            });
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException)
        {
            _failures.Add(new PipelineFailure()
            {
                Code = "BUILD_IO_FAILURE",
                Stage = "Pipeline",
                Summary =
                    "The Build pipeline could not complete a validated file operation.",
            });
        }

        return await FinishAsync(cleanupToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recovers interrupted version state, cleans artifacts, and begins any new transaction.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels initialization I/O.</param>
    /// <returns>A task that completes after the safe initialization boundary.</returns>
    internal async Task RecoverAndCleanAsync(
        CancellationToken cancellationToken)
    {
        await TemporaryMsBuildVersionTransaction.RecoverAsync(
                inputs.RootDirectory,
                inputs.ArtifactRootDirectory,
                inputs.MsBuildVersion!,
                cancellationToken)
            .ConfigureAwait(false);
        var artifactRoot = Path.GetFullPath(
            inputs.ArtifactRootDirectory.FullName);

        if (Directory.Exists(artifactRoot))
        {
            inputs.ArtifactRootDirectory.Delete(recursive: true);
        }

        Directory.CreateDirectory(artifactRoot);

        if (inputs.MsBuildVersion!.TargetVersion is null)
        {
            _initializationCompleted = true;
            return;
        }

        _versionTransaction =
            await TemporaryMsBuildVersionTransaction.BeginAsync(
                inputs.RootDirectory,
                inputs.ArtifactRootDirectory,
                inputs.MsBuildVersion)
            .ConfigureAwait(false);
        _initializationCompleted = true;
    }

    /// <summary>
    /// Resolves resources and performs only an explicitly authorized editor write.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels resource I/O.</param>
    /// <returns>A task that completes after resource preparation.</returns>
    internal async Task PrepareResourcesAsync(
        CancellationToken cancellationToken)
    {
        _ = await resourceComposer.WriteEditorConfigAsync(
                inputs.RootDirectory,
                options.Resources,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs explicitly enabled formatting.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels formatting.</param>
    /// <returns>Whether formatting succeeded or was skipped.</returns>
    internal async Task<bool> RunFormatAsync(
        CancellationToken cancellationToken)
    {
        if (!options.RunFormat)
        {
            return true;
        }

        foreach (var target in inputs.FormatTargets)
        {
            var arguments = new List<string>()
            {
                "format",
                target.File.FullName,
            };

            if (target.Scope != FormatScope.All)
            {
                var diffArguments = target.Scope
                    == FormatScope.WorkingTreeTracked
                    ? new[]
                    {
                        "diff",
                        "--name-only",
                        "-z",
                        "--diff-filter=ACMRTUXB",
                    }
                    :
                    [
                        "diff",
                        "--name-only",
                        "-z",
                        "--diff-filter=ACMRTUXB",
                        "HEAD",
                    ];
                var diff = await commandExecutor.ExecuteAsync(
                        new CommandRequest(
                            "git",
                            diffArguments,
                            inputs.RootDirectory.FullName,
                            TimeSpan.FromMinutes(1)),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!Succeeded(diff))
                {
                    AddFailure("FORMAT_SCOPE_FAILED", target.File.Name);
                    return false;
                }

                var sourcePaths = diff.StandardOutput
                    .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                    .Where(path => path.EndsWith(
                                       ".cs",
                                       StringComparison.OrdinalIgnoreCase)
                                   || path.EndsWith(
                                       ".vb",
                                       StringComparison.OrdinalIgnoreCase))
                    .Select(path => PathSafety.ResolveRelativeFile(
                        inputs.RootDirectory.FullName,
                        path,
                        mustExist: true))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                if (sourcePaths.Length == 0)
                {
                    continue;
                }

                arguments.Add("--include");
                arguments.AddRange(sourcePaths);
            }

            arguments.AddRange(target.Arguments);
            var result = await commandExecutor.ExecuteAsync(
                    new CommandRequest(
                        "dotnet",
                        arguments,
                        inputs.RootDirectory.FullName,
                        target.Timeout),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode != 0 || result.TimedOut || result.Cancelled)
            {
                AddFailure(
                    "FORMAT_FAILED",
                    Path.GetFileNameWithoutExtension(target.File.Name));
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Runs declared build targets with bounded parallelism.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels build commands.</param>
    /// <returns>Whether every build target succeeded.</returns>
    internal async Task<bool> RunBuildTargetsAsync(
        CancellationToken cancellationToken)
    {
        var results = await RunBoundedAsync(
                inputs.BuildTargets,
                RunBuildTargetAsync,
                cancellationToken)
            .ConfigureAwait(false);
        _targets.AddRange(results);
        return results.All(result => result.Succeeded);
    }

    private async Task<TargetExecutionResult> RunBuildTargetAsync(
        BuildTarget target,
        CancellationToken cancellationToken)
    {
        if (target.CleanBeforeBuild)
        {
            var cleanResult = await ExecuteDotNetAsync(
                    [
                        "clean",
                        target.File.FullName,
                        "--configuration",
                        target.Configuration,
                    ],
                    target.Timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!Succeeded(cleanResult))
            {
                AddFailure("CLEAN_FAILED", target.Name);
                var failureLogPath = await WriteFailureLogAsync(
                        target.Name,
                        "clean",
                        cleanResult,
                        cancellationToken)
                    .ConfigureAwait(false);
                return FailedTarget(
                    target.Name,
                    target.Configuration,
                    cleanResult,
                    failureLogPath);
            }
        }

        var arguments = new List<string>()
        {
            "build",
            target.File.FullName,
            "--configuration",
            target.Configuration,
            "-p:GeneratePackageOnBuild=false",
        };
        arguments.AddRange(target.Arguments);
        var result = await ExecuteDotNetAsync(
                arguments,
                target.Timeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (!Succeeded(result))
        {
            AddFailure("BUILD_TARGET_FAILED", target.Name);
            var failureLogPath = await WriteFailureLogAsync(
                    target.Name,
                    "build",
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            return FailedTarget(
                target.Name,
                target.Configuration,
                result,
                failureLogPath);
        }

        return new TargetExecutionResult()
        {
            Name = target.Name,
            Configuration = target.Configuration,
            Succeeded = true,
            ExitCode = 0,
        };
    }

    /// <summary>
    /// Runs and validates declared test targets.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels test commands.</param>
    /// <returns>Whether every test target succeeded with fresh non-empty TRX data.</returns>
    internal async Task<bool> RunTestTargetsAsync(
        CancellationToken cancellationToken)
    {
        var results = await RunBoundedAsync(
                inputs.TestTargets,
                RunTestTargetAsync,
                cancellationToken)
            .ConfigureAwait(false);
        _tests.AddRange(results);
        return results.All(result => result.Succeeded);
    }

    private async Task<TestExecutionResult> RunTestTargetAsync(
        TestTarget target,
        CancellationToken cancellationToken)
    {
        var resultDirectory = Path.Combine(
            inputs.ArtifactRootDirectory.FullName,
            options.Conventions.Layout.TestDirectoryName,
            SafeFileName(target.Name));

        if (Directory.Exists(resultDirectory))
        {
            Directory.Delete(resultDirectory, recursive: true);
        }

        Directory.CreateDirectory(resultDirectory);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var trxName = $"{SafeFileName(target.Name)}.trx";
        var arguments = CreateTestArguments(
            target,
            resultDirectory,
            trxName);
        var commandResult = await ExecuteDotNetAsync(
                arguments,
                target.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        var trxFiles = Directory
            .EnumerateFiles(resultDirectory, "*.trx", SearchOption.AllDirectories)
            .Where(path => File.GetLastWriteTimeUtc(path)
                           >= startedAtUtc.UtcDateTime.AddSeconds(-2))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var parseFailed = false;

        try
        {
            foreach (var trxPath in trxFiles)
            {
                var counts = ParseTrx(trxPath);
                passed += counts.Passed;
                failed += counts.Failed;
                skipped += counts.Skipped;
            }
        }
        catch (InvalidDataException)
        {
            parseFailed = true;
        }

        var succeeded = Succeeded(commandResult)
                        && trxFiles.Length != 0
                        && !parseFailed
                        && passed + failed + skipped != 0
                        && failed == 0;

        if (!succeeded)
        {
            AddFailure("TEST_TARGET_FAILED", target.Name);
            _ = await WriteFailureLogAsync(
                    target.Name,
                    "test",
                    commandResult,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new TestExecutionResult()
        {
            Name = target.Name,
            Command = target.Command,
            Configuration = target.Configuration,
            Succeeded = succeeded,
            Total = passed + failed + skipped,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            ResultPaths = trxFiles
                .Select(ToArtifactRelativePath)
                .ToArray(),
        };
    }

    /// <summary>
    /// Runs declared explicit pack targets.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels pack commands.</param>
    /// <returns>Whether every pack target succeeded.</returns>
    internal async Task<bool> RunPackTargetsAsync(
        CancellationToken cancellationToken)
    {
        var nugetDirectory = Path.Combine(
            inputs.ArtifactRootDirectory.FullName,
            options.Conventions.Layout.NuGetDirectoryName);
        Directory.CreateDirectory(nugetDirectory);
        var results = await RunBoundedAsync(
                inputs.PackTargets,
                async (target, token) =>
                {
                    var arguments = new List<string>()
                    {
                        "pack",
                        target.File.FullName,
                        "--configuration",
                        target.Configuration,
                        "--output",
                        nugetDirectory,
                        $"-p:PackageVersion={NormalizePackageVersion(target.PackageVersion)}",
                    };
                    arguments.AddRange(target.Arguments);
                    var result = await ExecuteDotNetAsync(
                            arguments,
                            target.Timeout,
                            token)
                        .ConfigureAwait(false);
                    var succeeded = Succeeded(result);

                    if (!succeeded)
                    {
                        AddFailure("PACK_TARGET_FAILED", target.Name);
                    }

                    var failureLogPath = succeeded
                        ? null
                        : await WriteFailureLogAsync(
                                target.Name,
                                "pack",
                                result,
                                token)
                            .ConfigureAwait(false);
                    return new TargetExecutionResult()
                    {
                        Name = target.Name,
                        Configuration = target.Configuration,
                        Succeeded = succeeded,
                        ExitCode = result.ExitCode,
                        FailureLogPath = failureLogPath,
                    };
                },
                cancellationToken)
            .ConfigureAwait(false);
        _targets.AddRange(results);
        return results.All(result => result.Succeeded);
    }

    /// <summary>
    /// Runs declared local publish targets and creates deterministic archives.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels publish commands.</param>
    /// <returns>Whether every publish target succeeded.</returns>
    internal async Task<bool> RunPublishTargetsAsync(
        CancellationToken cancellationToken)
    {
        foreach (var target in inputs.PublishTargets)
        {
            var rawDirectory = Path.Combine(
                inputs.ArtifactRootDirectory.FullName,
                $".publish-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rawDirectory);
            var arguments = new List<string>()
            {
                "publish",
                target.File.FullName,
                "--configuration",
                target.Configuration,
                "--output",
                rawDirectory,
            };

            if (target.Framework is not null)
            {
                arguments.Add("--framework");
                arguments.Add(target.Framework);
            }

            if (target.RuntimeIdentifier is not null)
            {
                arguments.Add("--runtime");
                arguments.Add(target.RuntimeIdentifier);
                arguments.Add("--self-contained");
                arguments.Add(target.SelfContained!.Value ? "true" : "false");
            }

            arguments.AddRange(target.Arguments);
            var result = await ExecuteDotNetAsync(
                    arguments,
                    target.Timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!Succeeded(result)
                || !Directory.EnumerateFiles(
                        rawDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .Any())
            {
                AddFailure("PUBLISH_TARGET_FAILED", target.Name);
                var failureLogPath = await WriteFailureLogAsync(
                        target.Name,
                        "publish",
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
                _targets.Add(new TargetExecutionResult()
                {
                    Name = target.Name,
                    Configuration = target.Configuration,
                    Succeeded = false,
                    ExitCode = result.ExitCode,
                    FailureLogPath = failureLogPath,
                });
                Directory.Delete(rawDirectory, recursive: true);
                return false;
            }

            var publishDirectory = Path.Combine(
                inputs.ArtifactRootDirectory.FullName,
                options.Conventions.Layout.PublishDirectoryName);
            Directory.CreateDirectory(publishDirectory);
            var archiveName = GetArchiveName(target);
            var archivePath = Path.Combine(publishDirectory, archiveName);
            await CreateDeterministicArchiveAsync(
                    rawDirectory,
                    archivePath,
                    cancellationToken)
                .ConfigureAwait(false);
            Directory.Delete(rawDirectory, recursive: true);
            _targets.Add(new TargetExecutionResult()
            {
                Name = target.Name,
                Configuration = target.Configuration,
                Succeeded = true,
                ExitCode = result.ExitCode,
            });
        }

        return true;
    }

    /// <summary>
    /// Inventories artifacts, restores version state, and commits the final manifest when required.
    /// </summary>
    /// <param name="cleanupToken">The independent cleanup token.</param>
    /// <returns>The frozen Build result.</returns>
    internal async Task<BuildResult> FinishAsync(
        CancellationToken cleanupToken)
    {
        if (!_initializationCompleted)
        {
            return CreateResult();
        }

        await CollectArtifactsAsync(cleanupToken)
            .ConfigureAwait(false);

        if (_versionTransaction is not null)
        {
            try
            {
                await _versionTransaction.RestoreAsync(cleanupToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException)
            {
                AddFailure("VERSION_RESTORE_FAILED", null);
            }
        }

        var writesManifest = profile is BuildExecutionProfile.Validate
            or BuildExecutionProfile.Pack
            or BuildExecutionProfile.Publish;
        var sourceTreeDirty = writesManifest
            && await IsSourceTreeDirtyAsync(cleanupToken)
                .ConfigureAwait(false);
        var result = CreateResult();

        if (writesManifest)
        {
            var packageVersions = _artifacts
                .Where(artifact => artifact.Kind
                    == PipelineArtifactKind.NuGetPackage)
                .Select(artifact => artifact.PackageVersion)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var manifest = new PipelineArtifactManifest()
            {
                Status = result.Status,
                SourceRevision = inputs.SourceRevision,
                SourceTreeDirty = sourceTreeDirty,
                PackageVersion = packageVersions.SingleOrDefault(),
                Targets = result.Targets,
                Tests = result.Tests,
                Artifacts = result.Artifacts,
                Failures = result.Failures,
            };
            await manifestWriter.WriteAsync(
                    inputs.ArtifactRootDirectory,
                    options.Conventions.Layout.ManifestFileName,
                    manifest,
                    cleanupToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private BuildResult CreateResult()
    {
        return new()
        {
            Status = _failures.Count == 0
                ? PipelineRunStatus.Succeeded
                : PipelineRunStatus.Failed,
            Targets = _targets.ToArray(),
            Tests = _tests.ToArray(),
            Artifacts = _artifacts.ToArray(),
            Failures = _failures.ToArray(),
            GeneratedDescription = _generatedDescription,
        };
    }

    /// <summary>
    /// Invokes an explicitly enabled optional description generator with bounded local data.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels local diff collection and generation.</param>
    /// <returns>Whether generation succeeded, skipped, or was allowed to continue.</returns>
    /// <exception cref="InvalidDataException">The local Git description scope is unavailable.</exception>
    internal async Task<bool> GenerateDescriptionAsync(
        CancellationToken cancellationToken)
    {
        if (!options.GenerateChangeDescriptions
            || descriptionGenerator is null
            || profile is not (
                BuildExecutionProfile.LocalBuild
                or BuildExecutionProfile.Message))
        {
            return true;
        }

        try
        {
            var kind = profile == BuildExecutionProfile.LocalBuild
                ? ChangeDescriptionKind.CommitMessage
                : ChangeDescriptionKind.ChangeRequest;
            var instructions = kind == ChangeDescriptionKind.CommitMessage
                ? await resourceComposer.GetCommitMessageInstructionsAsync(
                        inputs.RootDirectory,
                        options.Resources,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await resourceComposer.GetChangeRequestInstructionsAsync(
                        inputs.RootDirectory,
                        options.Resources,
                        cancellationToken)
                    .ConfigureAwait(false);
            var diffArguments = kind == ChangeDescriptionKind.CommitMessage
                ? new[] { "diff", "--no-ext-diff", "--no-textconv", "HEAD", }
                :
                [
                    "diff",
                    "--no-ext-diff",
                    "--no-textconv",
                    $"refs/remotes/{options.Conventions.GitRemoteName}/{options.Conventions.MainBranch}...{inputs.SourceRevision}",
                ];
            var diffResult = await commandExecutor.ExecuteAsync(
                    new CommandRequest(
                        "git",
                        diffArguments,
                        inputs.RootDirectory.FullName,
                        TimeSpan.FromMinutes(1)),
                    cancellationToken)
                .ConfigureAwait(false);
            var pathArguments = kind == ChangeDescriptionKind.CommitMessage
                ? new[] { "status", "--porcelain=v1", "-z", }
                :
                [
                    "diff",
                    "--name-only",
                    "-z",
                    $"refs/remotes/{options.Conventions.GitRemoteName}/{options.Conventions.MainBranch}...{inputs.SourceRevision}",
                ];
            var pathsResult = await commandExecutor.ExecuteAsync(
                    new CommandRequest(
                        "git",
                        pathArguments,
                        inputs.RootDirectory.FullName,
                        TimeSpan.FromMinutes(1)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!Succeeded(diffResult) || !Succeeded(pathsResult))
            {
                throw new InvalidDataException(
                    "The local Git description scope is unavailable.");
            }

            var changedPaths = pathsResult.StandardOutput
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => kind == ChangeDescriptionKind.CommitMessage
                    && entry.Length > 3
                        ? entry[3..]
                        : entry)
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var (diff, truncated) = TruncateUtf8(
                diffResult.StandardOutput,
                1024 * 1024);

            if (changedPaths.Length == 0 && string.IsNullOrEmpty(diff))
            {
                return true;
            }

            var generated = await descriptionGenerator.GenerateAsync(
                    new ChangeDescriptionRequest()
                    {
                        Kind = kind,
                        SourceRevision = inputs.SourceRevision,
                        TargetRevision = kind
                            == ChangeDescriptionKind.ChangeRequest
                            ? options.Conventions.MainBranch
                            : null,
                        ChangedPaths = changedPaths,
                        Diff = diff,
                        DiffTruncated = truncated,
                        Instructions = instructions.Content,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (generated is null)
            {
                return true;
            }

            _generatedDescription = ValidateDescription(generated);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or InvalidOperationException)
        {
            if (options.ChangeDescriptionFailureMode
                == ChangeDescriptionFailureMode.FailPipeline)
            {
                AddFailure("DESCRIPTION_FAILED", null);
                return false;
            }

            return true;
        }
    }

    private static ChangeDescription ValidateDescription(
        ChangeDescription description)
    {
        var subject = description.Subject.Trim();

        if (subject.Length is < 1 or > 256
            || subject.Contains('\n', StringComparison.Ordinal)
            || subject.Contains('\r', StringComparison.Ordinal)
            || subject.Any(char.IsControl))
        {
            throw new InvalidDataException(
                "The generated description subject is invalid.");
        }

        var body = description.Body
            ?.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (body is not null
            && (body.Contains('\0', StringComparison.Ordinal)
                || System.Text.Encoding.UTF8.GetByteCount(body) > 64 * 1024))
        {
            throw new InvalidDataException(
                "The generated description body is invalid.");
        }

        return description with
        {
            Subject = subject,
            Body = body,
        };
    }

    private static (string Text, bool Truncated) TruncateUtf8(
        string text,
        int maximumBytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(text) <= maximumBytes)
        {
            return (text, false);
        }

        var builder = new System.Text.StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            var candidate = builder.Length == 0
                ? line
                : $"\n{line}";

            if (System.Text.Encoding.UTF8.GetByteCount(builder.ToString())
                + System.Text.Encoding.UTF8.GetByteCount(candidate)
                > maximumBytes)
            {
                break;
            }

            builder.Append(candidate);
        }

        return (builder.ToString(), true);
    }

    /// <summary>
    /// Inventories all durable artifacts before temporary version state is restored.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels artifact hashing.</param>
    /// <returns>A task that completes after the artifact inventory is frozen.</returns>
    internal async Task CollectArtifactsAsync(
        CancellationToken cancellationToken)
    {
        if (_artifactsCollected)
        {
            return;
        }

        _artifacts.Clear();
        var root = inputs.ArtifactRootDirectory.FullName;

        foreach (var path in Directory
             .EnumerateFiles(root, "*", SearchOption.AllDirectories)
             .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (Path.GetFileName(path).Equals(
                    options.Conventions.Layout.ManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = ToArtifactRelativePath(path);
            var kind = ClassifyArtifact(relativePath);

            if (kind is null)
            {
                AddFailure("UNCLASSIFIED_ARTIFACT", "ArtifactCollection");
                File.Delete(path);
                continue;
            }

            var artifactKind = kind.Value;
            (string? Id, string? Version) identity;

            try
            {
                identity = artifactKind is PipelineArtifactKind.NuGetPackage
                    or PipelineArtifactKind.SymbolPackage
                    ? ReadPackageIdentity(path)
                    : default;
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                or InvalidOperationException)
            {
                AddFailure("PACKAGE_OUTPUT_INVALID", "Pack");
                File.Delete(path);
                continue;
            }

            var publishTarget = artifactKind == PipelineArtifactKind.PublishArchive
                ? FindPublishTarget(Path.GetFileName(path))
                : null;
            var stream = File.OpenRead(path);
            await using var streamLease = stream.ConfigureAwait(false);
            var hash = Convert.ToHexStringLower(
                    await SHA256.HashDataAsync(stream, cancellationToken)
                        .ConfigureAwait(false));
            var file = new FileInfo(path);
            _artifacts.Add(new PipelineArtifact()
            {
                RelativePath = relativePath,
                Kind = artifactKind,
                LogicalName = identity.Id
                              ?? publishTarget?.ArtifactName
                              ?? ResolveFailureLogicalName(relativePath)
                              ?? ResolveLogicalName(relativePath, artifactKind),
                Sha256 = hash,
                Size = file.Length,
                PackageId = identity.Id,
                PackageVersion = identity.Version,
                TargetFramework = publishTarget?.Framework,
                RuntimeIdentifier = publishTarget?.RuntimeIdentifier,
            });
        }

        ValidateCollectedPackages();
        _artifacts.Sort(static (left, right) =>
        {
            var kindComparison = left.Kind.CompareTo(right.Kind);
            return kindComparison != 0
                ? kindComparison
                : StringComparer.Ordinal.Compare(
                    left.RelativePath,
                    right.RelativePath);
        });
        _artifactsCollected = true;
    }

    private void ValidateCollectedPackages()
    {
        if (profile is not (
            BuildExecutionProfile.Pack
            or BuildExecutionProfile.Publish)
            || inputs.PackTargets.Count == 0)
        {
            return;
        }

        var primaryPackages = _artifacts
            .Where(artifact =>
                artifact.Kind == PipelineArtifactKind.NuGetPackage)
            .ToArray();
        var expectedVersion = NormalizePackageVersion(
            inputs.PackTargets[0].PackageVersion);
        var valid = primaryPackages.Length == inputs.PackTargets.Count
                    && primaryPackages.All(artifact => string.Equals(
                        artifact.PackageVersion,
                        expectedVersion,
                        StringComparison.Ordinal))
                    && primaryPackages.Select(artifact => artifact.PackageId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count() == primaryPackages.Length
                    && ValidateCompiledPackageVersions(primaryPackages);

        if (valid)
        {
            return;
        }

        AddFailure("PACKAGE_OUTPUT_INVALID", "Pack");

        foreach (var artifact in _artifacts
                     .Where(artifact => artifact.Kind is
                         PipelineArtifactKind.NuGetPackage
                         or PipelineArtifactKind.SymbolPackage)
                     .ToArray())
        {
            var path = Path.Combine(
                inputs.ArtifactRootDirectory.FullName,
                artifact.RelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            File.Delete(path);
            _artifacts.Remove(artifact);
        }
    }

    private bool ValidateCompiledPackageVersions(
        IReadOnlyList<PipelineArtifact> primaryPackages)
    {
        var targetVersion = inputs.MsBuildVersion!.TargetVersion;

        if (targetVersion is null)
        {
            return true;
        }

        var parsed = DailyReleaseVersionPolicy.Parse(targetVersion);
        var expectedAssemblyVersion = parsed.Counter == 0
            ? $"{targetVersion}.0"
            : targetVersion;

        foreach (var package in primaryPackages)
        {
            var packagePath = Path.Combine(
                inputs.ArtifactRootDirectory.FullName,
                package.RelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

            if (!ValidatePackageAssemblies(
                    packagePath,
                    package.PackageId!,
                    targetVersion,
                    expectedAssemblyVersion))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidatePackageAssemblies(
        string packagePath,
        string packageId,
        string expectedPackageVersion,
        string expectedAssemblyVersion)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var assemblyEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith(
                                "lib/",
                                StringComparison.OrdinalIgnoreCase)
                            && Path.GetFileName(entry.FullName).Equals(
                                $"{packageId}.dll",
                                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var entry in assemblyEntries)
        {
            using var assemblyBytes = new MemoryStream();
            using (var entryStream = entry.Open())
            {
                entryStream.CopyTo(assemblyBytes);
            }

            assemblyBytes.Position = 0;

            try
            {
                using var peReader = new PEReader(assemblyBytes);
                var metadata = peReader.GetMetadataReader();
                var assembly = metadata.GetAssemblyDefinition();
                var fileVersion = ReadAssemblyAttribute(
                    metadata,
                    assembly,
                    "AssemblyFileVersionAttribute");
                var informationalVersion = ReadAssemblyAttribute(
                    metadata,
                    assembly,
                    "AssemblyInformationalVersionAttribute");

                if (!string.Equals(
                        assembly.Version.ToString(),
                        expectedAssemblyVersion,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        fileVersion,
                        expectedAssemblyVersion,
                        StringComparison.Ordinal)
                    || !IsValidInformationalVersion(
                        informationalVersion,
                        expectedPackageVersion))
                {
                    return false;
                }
            }
            catch (Exception exception) when (
                exception is BadImageFormatException
                or InvalidOperationException)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidInformationalVersion(
        string? informationalVersion,
        string expectedPackageVersion)
    {
        if (informationalVersion is null)
        {
            return false;
        }

        var parts = informationalVersion.Split('+', 2);

        return string.Equals(
                   parts[0],
                   expectedPackageVersion,
                   StringComparison.Ordinal)
               && (parts.Length == 1
                   || string.Equals(
                       parts[1],
                       inputs.SourceRevision,
                       StringComparison.Ordinal));
    }

    private static string? ReadAssemblyAttribute(
        MetadataReader reader,
        in AssemblyDefinition assembly,
        string attributeName)
    {
        foreach (var handle in assembly.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);

            if (!string.Equals(
                    GetAttributeTypeName(reader, attribute.Constructor),
                    attributeName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var value = reader.GetBlobReader(attribute.Value);

            if (value.ReadUInt16() != 1)
            {
                return null;
            }

            return value.ReadSerializedString();
        }

        return null;
    }

    private static string? GetAttributeTypeName(
        MetadataReader reader,
        in EntityHandle constructor)
    {
        EntityHandle typeHandle;

        if (constructor.Kind == HandleKind.MemberReference)
        {
            typeHandle = reader.GetMemberReference(
                    (MemberReferenceHandle)constructor)
                .Parent;
        }
        else if (constructor.Kind == HandleKind.MethodDefinition)
        {
            typeHandle = reader.GetMethodDefinition(
                    (MethodDefinitionHandle)constructor)
                .GetDeclaringType();
        }
        else
        {
            return null;
        }

        return typeHandle.Kind switch
        {
            HandleKind.TypeReference => reader.GetString(
                reader.GetTypeReference((TypeReferenceHandle)typeHandle).Name),
            HandleKind.TypeDefinition => reader.GetString(
                reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle).Name),
            _ => null,
        };
    }

    private DotnetPublishTarget? FindPublishTarget(string fileName)
    {
        return inputs.PublishTargets.SingleOrDefault(target =>
            fileName.Equals(
                GetArchiveName(target),
                StringComparison.OrdinalIgnoreCase));
    }

    private Task<CommandResult> ExecuteDotNetAsync(
        IReadOnlyList<string> arguments,
        in TimeSpan timeout,
        in CancellationToken cancellationToken)
    {
        return commandExecutor.ExecuteAsync(
                new CommandRequest(
                    "dotnet",
                    arguments,
                    inputs.RootDirectory.FullName,
                    timeout),
                cancellationToken);
    }

    private async Task<IReadOnlyList<TResult>> RunBoundedAsync<TTarget, TResult>(
        IReadOnlyList<TTarget> targets,
        Func<TTarget, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(
            options.MaxDegreeOfParallelism);
        var tasks = targets.Select(
                async (target, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        return (index, Result: await action(
                                target,
                                cancellationToken)
                            .ConfigureAwait(false));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
            .ToArray();
        return (await Task.WhenAll(tasks).ConfigureAwait(false))
            .OrderBy(item => item.index)
            .Select(item => item.Result)
            .ToArray();
    }

    private static List<string> CreateTestArguments(
        TestTarget target,
        string resultDirectory,
        string trxName)
    {
        var arguments = new List<string>();

        if (target.Command == DotNetTestCommand.MicrosoftTestingPlatformRun)
        {
            arguments.AddRange(
            [
                "run",
                "--project",
                target.File.FullName,
                "--configuration",
                target.Configuration,
            ]);
            arguments.AddRange(target.CommandArguments);
            arguments.Add("--");
            arguments.Add("--report-trx");
            arguments.Add("--report-trx-filename");
            arguments.Add(trxName);
            arguments.Add("--report-trx-directory");
            arguments.Add(resultDirectory);
            arguments.AddRange(target.RunnerArguments);
            return arguments;
        }

        arguments.Add("test");

        if (target.Command
            == DotNetTestCommand.MicrosoftTestingPlatformTest)
        {
            arguments.Add("--project");
        }

        arguments.Add(target.File.FullName);
        arguments.Add("--configuration");
        arguments.Add(target.Configuration);
        arguments.Add("--results-directory");
        arguments.Add(resultDirectory);
        arguments.AddRange(target.CommandArguments);

        if (target.Command == DotNetTestCommand.VSTest)
        {
            arguments.Add("--logger");
            arguments.Add($"trx;LogFileName={trxName}");
        }
        else
        {
            arguments.Add("--");
            arguments.Add("--report-trx");
            arguments.Add("--report-trx-filename");
            arguments.Add(trxName);
            arguments.AddRange(target.RunnerArguments);
        }

        return arguments;
    }

    private static (int Passed, int Failed, int Skipped) ParseTrx(
        string path)
    {
        try
        {
            var document = XDocument.Load(path);
            var results = document
                .Descendants()
                .Where(element => element.Name.LocalName == "UnitTestResult")
                .ToArray();
            return (
                results.Count(element => string.Equals(
                    (string?)element.Attribute("outcome"),
                    "Passed",
                    StringComparison.OrdinalIgnoreCase)),
                results.Count(element =>
                    !string.Equals(
                        (string?)element.Attribute("outcome"),
                        "Passed",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        (string?)element.Attribute("outcome"),
                        "NotExecuted",
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        (string?)element.Attribute("outcome"),
                        "Inconclusive",
                        StringComparison.OrdinalIgnoreCase)),
                results.Count(element =>
                    string.Equals(
                        (string?)element.Attribute("outcome"),
                        "NotExecuted",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        (string?)element.Attribute("outcome"),
                        "Inconclusive",
                        StringComparison.OrdinalIgnoreCase)));
        }
        catch (System.Xml.XmlException exception)
        {
            throw new InvalidDataException(
                $"Test result '{Path.GetFileName(path)}' is malformed.",
                exception);
        }
    }

    private static async Task CreateDeterministicArchiveAsync(
        string sourceDirectory,
        string archivePath,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{archivePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var output = File.Create(temporaryPath);
            await using (output.ConfigureAwait(false))
            {
                var archive = new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: false);
                await using (archive.ConfigureAwait(false))
                {
                    foreach (var path in Directory
                                 .EnumerateFiles(
                                     sourceDirectory,
                                     "*",
                                     SearchOption.AllDirectories)
                                 .OrderBy(
                                     path => Path.GetRelativePath(
                                         sourceDirectory,
                                         path),
                                     StringComparer.Ordinal))
                    {
                        _ = PathSafety.EnsureStrictDescendant(
                            sourceDirectory,
                            new FileInfo(path),
                            mustExist: true);
                        var relativePath = Path.GetRelativePath(
                                sourceDirectory,
                                path)
                            .Replace('\\', '/');
                        var entry = archive.CreateEntry(
                            relativePath,
                            CompressionLevel.Optimal);
                        entry.LastWriteTime =
                            new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        var input = File.OpenRead(path);
                        await using var inputLease =
                            input.ConfigureAwait(false);
                        var entryStream =
                            await entry.OpenAsync(cancellationToken)
                                .ConfigureAwait(false);
                        await using var entryStreamLease =
                            entryStream.ConfigureAwait(false);
                        await input.CopyToAsync(
                                entryStream,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            File.Move(temporaryPath, archivePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<bool> IsSourceTreeDirtyAsync(
        CancellationToken cancellationToken)
    {
        var result = await commandExecutor.ExecuteAsync(
                new CommandRequest(
                    "git",
                    ["status", "--porcelain=v1", "-z", "--untracked-files=all",],
                    inputs.RootDirectory.FullName,
                    TimeSpan.FromMinutes(1)),
                cancellationToken)
            .ConfigureAwait(false);

        if (!Succeeded(result))
        {
            AddFailure("SOURCE_STATUS_FAILED", "Manifest");
            return true;
        }

        var artifactRelative = Path.GetRelativePath(
                inputs.RootDirectory.FullName,
                inputs.ArtifactRootDirectory.FullName)
            .Replace('\\', '/')
            .TrimEnd('/');
        foreach (var entry in result.StandardOutput.Split(
                     '\0',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Length < 4)
            {
                return true;
            }

            var status = entry[..2];
            var path = entry[3..].Replace('\\', '/');

            if (status == "??"
                && (path.Equals(
                        artifactRelative,
                        StringComparison.Ordinal)
                    || path.StartsWith(
                        $"{artifactRelative}/",
                        StringComparison.Ordinal)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool Succeeded(CommandResult result)
    {
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled;
    }

    private static TargetExecutionResult FailedTarget(
        string name,
        string configuration,
        CommandResult result,
        string failureLogPath)
    {
        return new()
        {
            Name = name,
            Configuration = configuration,
            Succeeded = false,
            ExitCode = result.ExitCode,
            FailureLogPath = failureLogPath,
        };
    }

    private void AddFailure(string code, string? targetName)
    {
        lock (_failures)
        {
            _failures.Add(new PipelineFailure()
            {
                Code = code,
                Stage = targetName ?? "Pipeline",
                Summary = "A configured Build operation failed.",
            });
        }
    }

    /// <summary>
    /// Records a redacted stage failure so dependent producers are gated.
    /// </summary>
    /// <param name="exception">The failure category, whose message is never persisted.</param>
    internal void RecordStageFailure(Exception exception)
    {
        AddFailure(
            exception is OperationCanceledException
                ? "BUILD_CANCELLED"
                : "BUILD_UNEXPECTED_FAILURE",
            "Pipeline");
    }

    private string ToArtifactRelativePath(string path)
    {
        return Path.GetRelativePath(
                inputs.ArtifactRootDirectory.FullName,
                path)
            .Replace('\\', '/');
    }

    private string? ResolveFailureLogicalName(string relativePath)
    {
        lock (_failureLogNames)
        {
            return _failureLogNames.GetValueOrDefault(relativePath);
        }
    }

    private async Task<string> WriteFailureLogAsync(
        string targetName,
        string commandKind,
        CommandResult result,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(
            inputs.ArtifactRootDirectory.FullName,
            "failures");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"{SafeFileName(targetName)}-{commandKind}.failure.txt");
        var content =
            $"stage={targetName}\n"
            + $"commandKind={commandKind}\n"
            + $"exitCode={result.ExitCode}\n"
            + $"timedOut={result.TimedOut}\n"
            + $"cancelled={result.Cancelled}\n"
            + "summary=The configured command did not succeed.\n";
        await File.WriteAllTextAsync(
                path,
                content,
                new System.Text.UTF8Encoding(false),
                cancellationToken)
            .ConfigureAwait(false);
        var relativePath = ToArtifactRelativePath(path);

        lock (_failureLogNames)
        {
            _failureLogNames[relativePath] = targetName;
        }

        return relativePath;
    }

    private PipelineArtifactKind? ClassifyArtifact(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        var firstSegment = normalizedPath.Split('/')[0];

        var isNuGetDirectory = firstSegment.Equals(
            options.Conventions.Layout.NuGetDirectoryName,
            StringComparison.OrdinalIgnoreCase);
        var isPublishDirectory = firstSegment.Equals(
            options.Conventions.Layout.PublishDirectoryName,
            StringComparison.OrdinalIgnoreCase);
        var isFailureDirectory = firstSegment.Equals(
            "failures",
            StringComparison.OrdinalIgnoreCase);
        var isTestDirectory = firstSegment.Equals(
            options.Conventions.Layout.TestDirectoryName,
            StringComparison.OrdinalIgnoreCase);
        return normalizedPath switch
        {
            _ when isNuGetDirectory
                   && normalizedPath.EndsWith(
                       ".snupkg",
                       StringComparison.OrdinalIgnoreCase) =>
                PipelineArtifactKind.SymbolPackage,
            _ when isNuGetDirectory
                   && normalizedPath.EndsWith(
                       ".nupkg",
                       StringComparison.OrdinalIgnoreCase) =>
                PipelineArtifactKind.NuGetPackage,
            _ when isPublishDirectory
                   && normalizedPath.EndsWith(
                       ".zip",
                       StringComparison.OrdinalIgnoreCase) =>
                PipelineArtifactKind.PublishArchive,
            _ when isFailureDirectory
                   && normalizedPath.EndsWith(
                       ".failure.txt",
                       StringComparison.OrdinalIgnoreCase) =>
                PipelineArtifactKind.FailureLog,
            _ when isTestDirectory => PipelineArtifactKind.TestResult,
            _ => null,
        };
    }

    private static (string? Id, string? Version) ReadPackageIdentity(
        string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(
            ".nuspec",
            StringComparison.OrdinalIgnoreCase));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var metadata = document.Descendants().Single(element =>
            element.Name.LocalName == "metadata");
        return (
            metadata.Elements().Single(element =>
                element.Name.LocalName == "id").Value,
            NormalizePackageVersion(
                metadata.Elements().Single(element =>
                    element.Name.LocalName == "version").Value));
    }

    private static string ResolveLogicalName(
        string relativePath,
        PipelineArtifactKind kind)
    {
        return kind switch
        {
            PipelineArtifactKind.PublishArchive =>
                Path.GetFileNameWithoutExtension(relativePath),
            _ => relativePath.Split('/').Skip(1).FirstOrDefault()
                 ?? Path.GetFileName(relativePath),
        };
    }

    private static string SafeFileName(string value)
    {
        return string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character)
                ? '_'
                : character));
    }

    private static string NormalizePackageVersion(string version)
    {
        return NuGetVersion.Parse(version).ToNormalizedString();
    }

    private static string GetArchiveName(DotnetPublishTarget target)
    {
        var suffix = string.Join(
            "-",
            new[] { target.Framework, target.RuntimeIdentifier, }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrEmpty(suffix)
            ? $"{target.ArtifactName}.zip"
            : $"{target.ArtifactName}-{suffix}.zip";
    }
}