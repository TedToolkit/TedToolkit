// -----------------------------------------------------------------------
// <copyright file="NuGetPublicationService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Pushes validated NuGet artifacts with optional trusted checkpoints.
/// </summary>
/// <param name="commandExecutor">The safe command boundary.</param>
/// <param name="secretResolver">The on-demand secret resolver.</param>
internal sealed class NuGetPublicationService(
    ICombineCommandExecutor commandExecutor,
    IPipelineSecretResolver secretResolver)
{
    /// <summary>
    /// Pushes all remaining packages in deterministic order.
    /// </summary>
    /// <param name="registration">The validated registration.</param>
    /// <param name="manifest">The successful clean manifest.</param>
    /// <param name="artifactRoot">The validated artifact root.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>The package-push results.</returns>
    /// <exception cref="InvalidDataException">
    /// The manifest, checkpoint, or resolved credential is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A package push or checkpoint operation fails.
    /// </exception>
    public async Task<IReadOnlyList<PackagePushResult>> PublishAsync(
        ValidatedCombineRegistration registration,
        PipelineArtifactManifest manifest,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        var options = registration.Options.NuGetPush!;
        var packages = manifest.Artifacts
            .Where(artifact =>
                artifact.Kind == PipelineArtifactKind.NuGetPackage)
            .OrderBy(
                artifact => artifact.PackageId,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                artifact => artifact.RelativePath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (packages.Length == 0)
        {
            throw new InvalidDataException(
                "PushNuGetPackages requires at least one NuGetPackage artifact.");
        }

        var manifestFile = new FileInfo(registration.ManifestPath!);
        var context = await CreateCheckpointContextAsync(
                manifest,
                manifestFile,
                packages,
                cancellationToken)
            .ConfigureAwait(false);
        var checkpoint = options.UsePackagePublicationCheckpoint
            ? registration.Options.PackagePublicationCheckpoint
                ?? throw new InvalidOperationException(
                    "The validated package checkpoint is unavailable.")
            : null;
        var completed = checkpoint is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : await ReadCompletedAsync(
                    checkpoint,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        string? apiKey = null;
        string? symbolApiKey = null;

        if (packages.Any(package =>
                !completed.Contains(package.PackageId!))
            && options.AuthenticationMode == NuGetAuthenticationMode.ApiKey)
        {
            apiKey = await secretResolver.ResolveAsync(
                    options.CredentialReference!,
                    cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidDataException(
                    "The NuGet credential reference could not be resolved.");
            }

            if (options.SymbolCredentialReference is null)
            {
                symbolApiKey = apiKey;
            }
            else
            {
                symbolApiKey = await secretResolver.ResolveAsync(
                        options.SymbolCredentialReference,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(symbolApiKey))
                {
                    throw new InvalidDataException(
                        "The NuGet symbol credential reference could not be resolved.");
                }
            }
        }

        var results = new List<PackagePushResult>();

        foreach (var package in packages)
        {
            if (completed.Contains(package.PackageId!))
            {
                continue;
            }

            var packagePath = ResolveAndValidateArtifact(
                artifactRoot,
                package);
            var arguments = CreateArguments(
                packagePath,
                options,
                apiKey,
                symbolApiKey,
                registration.RootPath);
            var commandResult = await commandExecutor.ExecuteAsync(
                    new CombineCommandRequest(
                        "dotnet",
                        arguments,
                        registration.RootPath,
                        options.Timeout),
                    apiKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (commandResult.ExitCode != 0
                || commandResult.TimedOut
                || commandResult.Cancelled)
            {
                throw new InvalidOperationException(
                    "The NuGet package command failed.");
            }

            var disposition = commandResult.Duplicate
                ? OperationDisposition.SkippedDuplicate
                : OperationDisposition.Created;
            results.Add(new PackagePushResult()
            {
                PackageId = package.PackageId!,
                Version = package.PackageVersion!,
                Source = options.Source!,
                Disposition = disposition,
            });

            if (checkpoint is not null)
            {
                if (disposition == OperationDisposition.SkippedDuplicate)
                {
                    throw new InvalidOperationException(
                        "A duplicate result cannot be checkpointed.");
                }

                var record = context.Packages.Single(item =>
                    item.PackageId.Equals(
                        package.PackageId,
                        StringComparison.OrdinalIgnoreCase));
                await checkpoint.RecordCompletedAsync(
                        context,
                        record,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (checkpoint is not null)
        {
            await checkpoint.CompleteAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }

        return results;
    }

    private static async Task<HashSet<string>> ReadCompletedAsync(
        IPackagePublicationCheckpoint checkpoint,
        PackagePublicationCheckpointContext context,
        CancellationToken cancellationToken)
    {
        var completed = await checkpoint.ReadCompletedPackageIdsAsync(
                context,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "The package checkpoint returned a null package set.");
        var known = context.Packages
            .Select(package => package.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var invalid = completed.Any(packageId =>
            string.IsNullOrWhiteSpace(packageId)
            || !known.Contains(packageId)
            || !result.Add(packageId));

        if (invalid)
        {
            throw new InvalidDataException(
                "The package checkpoint returned an invalid package ID.");
        }

        return result;
    }

    private static async Task<PackagePublicationCheckpointContext>
        CreateCheckpointContextAsync(
            PipelineArtifactManifest manifest,
            FileInfo manifestFile,
            IReadOnlyList<PipelineArtifact> packages,
            CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            manifestFile.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        string manifestHash;
        await using (stream.ConfigureAwait(false))
        {
            manifestHash = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken)
                    .ConfigureAwait(false));
        }

        var records = packages.Select(package =>
        {
            var symbol = manifest.Artifacts.SingleOrDefault(candidate =>
                candidate.Kind == PipelineArtifactKind.SymbolPackage
                && candidate.PackageId!.Equals(
                    package.PackageId,
                    StringComparison.OrdinalIgnoreCase)
                && candidate.PackageVersion!.Equals(
                    package.PackageVersion,
                    StringComparison.OrdinalIgnoreCase));
            return new PackagePublicationCheckpointRecord(
                package.PackageId!,
                package.Sha256,
                symbol?.Sha256);
        }).ToArray();
        return new(
            manifest.PackageVersion!,
            manifest.SourceRevision,
            manifestHash,
            records);
    }

    private static List<string> CreateArguments(
        string packagePath,
        NuGetPushOptions options,
        string? apiKey,
        string? symbolApiKey,
        string rootPath)
    {
        var arguments = new List<string>()
        {
            "nuget",
            "push",
            packagePath,
            "--source",
            options.Source!.AbsoluteUri,
        };

        if (options.AuthenticationMode == NuGetAuthenticationMode.ApiKey)
        {
            arguments.Add("--api-key");
            arguments.Add(apiKey!);
        }
        else if (options.ConfigFilePath is not null)
        {
            var configPath = Path.GetFullPath(
                Path.Combine(rootPath, options.ConfigFilePath));
            var rootPrefix = Path.TrimEndingDirectorySeparator(rootPath)
                             + Path.DirectorySeparatorChar;

            if (!configPath.StartsWith(
                    rootPrefix,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal)
                || !File.Exists(configPath))
            {
                throw new InvalidDataException(
                    "NuGet ConfigFilePath must identify a root-contained file.");
            }

            arguments.Add("--configfile");
            arguments.Add(configPath);
        }

        if (options.SymbolSource is not null)
        {
            arguments.Add("--symbol-source");
            arguments.Add(options.SymbolSource.AbsoluteUri);

            if (options.AuthenticationMode == NuGetAuthenticationMode.ApiKey)
            {
                arguments.Add("--symbol-api-key");
                arguments.Add(symbolApiKey!);
            }
        }

        if (options.SkipDuplicate)
        {
            arguments.Add("--skip-duplicate");
        }

        return arguments;
    }

    private static string ResolveAndValidateArtifact(
        DirectoryInfo artifactRoot,
        PipelineArtifact artifact)
    {
        var path = Path.GetFullPath(
            Path.Combine(
                artifactRoot.FullName,
                artifact.RelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        var info = new FileInfo(path);

        if (!info.Exists
            || info.LinkTarget is not null
            || info.Length != artifact.Size)
        {
            throw new InvalidDataException(
                "A NuGet artifact changed after manifest validation.");
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (!hash.Equals(artifact.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A NuGet artifact changed after manifest validation.");
        }

        return path;
    }
}