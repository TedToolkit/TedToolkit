// -----------------------------------------------------------------------
// <copyright file="PipelineArtifactManifestStore.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Build.Internal;

namespace TedToolkit.ModularPipelines.Build.Artifacts;

/// <summary>
/// Represents the strict deterministic schema-1 manifest reader and writer.
/// </summary>
public sealed class PipelineArtifactManifestStore : IPipelineArtifactManifestReader,
      IPipelineArtifactManifestWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        Converters =
        {
            new JsonStringEnumConverter(
                namingPolicy: null,
                allowIntegerValues: false),
        },
    };

    /// <inheritdoc/>
    public async Task<FileInfo> WriteAsync(
        DirectoryInfo artifactRoot,
        string manifestFileName,
        PipelineArtifactManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var rootPath = PathSafety.EnsureExistingRoot(artifactRoot);
        ValidateManifestFileName(manifestFileName);
        var manifestPath = Path.Combine(rootPath, manifestFileName);
        var normalized = Normalize(manifest);
        await ValidateAsync(
                rootPath,
                normalized,
                manifestPath,
                cancellationToken)
            .ConfigureAwait(false);

        var temporaryPath = Path.Combine(
            rootPath,
            $".{manifestFileName}.{Guid.NewGuid():N}.tmp");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            normalized,
            SerializerOptions);

        try
        {
            var temporaryStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (temporaryStream.ConfigureAwait(false))
            {
                await temporaryStream.WriteAsync(bytes, cancellationToken)
                    .ConfigureAwait(false);
                await temporaryStream.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return new FileInfo(manifestPath);
    }

    /// <inheritdoc/>
    public async Task<PipelineArtifactManifest> ReadAsync(
        DirectoryInfo artifactRoot,
        FileInfo manifestFile,
        ArtifactValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestFile);
        var consumerRootPath = PathSafety.EnsureExistingRoot(artifactRoot);
        var manifestPath = PathSafety.EnsureStrictDescendant(
            consumerRootPath,
            manifestFile,
            mustExist: true);
        var rootPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidDataException(
                "The manifest must have an artifact-root parent.");
        _ = PathSafety.EnsureExistingRoot(new DirectoryInfo(rootPath));

        var stream = File.OpenRead(manifestPath);
        await using var streamLease = stream.ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<PipelineArtifactManifest>(
                stream,
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The manifest JSON is empty.");
        await ValidateAsync(
                rootPath,
                manifest,
                manifestPath,
                cancellationToken)
            .ConfigureAwait(false);

        options ??= new();
        var expectedRevision = options.ExpectedSourceRevision
                               ?? await TryReadGitRevisionAsync(
                                       consumerRootPath,
                                       cancellationToken)
                                   .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(expectedRevision)
            && !string.Equals(
                manifest.SourceRevision,
                expectedRevision,
                StringComparison.Ordinal)
            && !options.AllowSourceRevisionMismatch)
        {
            throw new InvalidDataException(
                "The manifest source revision does not match the consumer revision.");
        }

        return manifest;
    }

    private static async Task ValidateAsync(
        string rootPath,
        PipelineArtifactManifest manifest,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported manifest schema '{manifest.SchemaVersion}'.");
        }

        ValidateText(manifest.SourceRevision, "source revision");

        if (!Enum.IsDefined(manifest.Status)
            || manifest.Status == PipelineRunStatus.Running)
        {
            throw new InvalidDataException(
                "A persisted manifest status must be Succeeded or Failed.");
        }

        if (manifest.CreatedAtUtc == default
            || manifest.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                "The manifest creation timestamp must be UTC.");
        }

        if (manifest.Status == PipelineRunStatus.Succeeded
            && manifest.Failures.Count != 0)
        {
            throw new InvalidDataException(
                "A successful manifest cannot contain failures.");
        }

        if (manifest.Targets is null
            || manifest.Tests is null
            || manifest.Artifacts is null
            || manifest.Failures is null)
        {
            throw new InvalidDataException(
                "Manifest collections cannot be null.");
        }

        if (manifest.Status == PipelineRunStatus.Failed
            && manifest.Failures.Count == 0
            && manifest.Targets.All(target => target.Succeeded)
            && manifest.Tests.All(test => test.Succeeded))
        {
            throw new InvalidDataException(
                "A failed manifest must contain failed results or a failure.");
        }

        var relativePaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var primaryPackageVersions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var primaryPackageIdentities = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var symbolPackageIdentities = new List<string>();

        var canonicalArtifactOrder = manifest.Artifacts
            .OrderBy(artifact => artifact.Kind)
            .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (!manifest.Artifacts.SequenceEqual(canonicalArtifactOrder))
        {
            throw new InvalidDataException(
                "Manifest artifacts are not in canonical kind/path order.");
        }

        foreach (var artifact in manifest.Artifacts)
        {
            ValidateText(artifact.LogicalName, "artifact logical name");

            if (!Enum.IsDefined(artifact.Kind)
                || artifact.RelativePath.Contains('\\', StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "An artifact kind or path is not canonical.");
            }

            var normalizedRelativePath = artifact.RelativePath;

            if (!relativePaths.Add(normalizedRelativePath))
            {
                throw new InvalidDataException(
                    $"Duplicate artifact path '{normalizedRelativePath}'.");
            }

            var artifactPath = PathSafety.ResolveRelativeFile(
                rootPath,
                normalizedRelativePath,
                mustExist: true);
            var file = new FileInfo(artifactPath);

            if (artifact.Size < 0 || file.Length != artifact.Size)
            {
                throw new InvalidDataException(
                    $"Artifact size mismatch for '{normalizedRelativePath}'.");
            }

            var artifactStream = file.OpenRead();
            await using var artifactStreamLease =
                artifactStream.ConfigureAwait(false);
            var hash = Convert.ToHexStringLower(
                    await SHA256.HashDataAsync(
                            artifactStream,
                            cancellationToken)
                        .ConfigureAwait(false));

            if (!IsSha256(artifact.Sha256)
                || !string.Equals(hash, artifact.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Artifact hash mismatch for '{normalizedRelativePath}'.");
            }

            if (artifact.Kind is PipelineArtifactKind.NuGetPackage
                or PipelineArtifactKind.SymbolPackage)
            {
                var identity = ReadPackageIdentity(artifactPath);
                var identityKey = string.Concat(
                    identity.Id,
                    "\0",
                    identity.Version);

                if (!string.Equals(
                        identity.Id,
                        artifact.PackageId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        identity.Version,
                        artifact.PackageVersion,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        identity.Id,
                        artifact.LogicalName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Package identity mismatch for '{normalizedRelativePath}'.");
                }

                ValidatePackageMetadata(artifact, normalizedRelativePath);

                if (artifact.Kind == PipelineArtifactKind.NuGetPackage)
                {
                    if (!primaryPackageIdentities.Add(identityKey))
                    {
                        throw new InvalidDataException(
                            $"Duplicate primary package identity '{identity.Id}'.");
                    }

                    primaryPackageVersions.Add(identity.Version);
                }
                else
                {
                    symbolPackageIdentities.Add(identityKey);
                }
            }
            else if (artifact.PackageId is not null
                     || artifact.PackageVersion is not null)
            {
                throw new InvalidDataException(
                    $"Non-package artifact '{normalizedRelativePath}' has package identity.");
            }

            ValidateKindMetadata(artifact, artifactPath);
        }

        ValidateResultRelations(manifest);

        if (primaryPackageVersions.Count > 1)
        {
            throw new InvalidDataException(
                "A manifest cannot contain mixed package versions.");
        }

        if (symbolPackageIdentities.Any(
                identity => !primaryPackageIdentities.Contains(identity)))
        {
            throw new InvalidDataException(
                "Every symbol package must match one primary package identity.");
        }

        var unexpectedVersionWithoutPrimary =
            primaryPackageVersions.Count == 0
            && manifest.PackageVersion is not null;
        var mismatchingPrimaryVersion =
            primaryPackageVersions.Count == 1
            && !string.Equals(
                primaryPackageVersions.Single(),
                manifest.PackageVersion,
                StringComparison.Ordinal);

        if (unexpectedVersionWithoutPrimary || mismatchingPrimaryVersion)
        {
            throw new InvalidDataException(
                "The manifest package version does not match its packages.");
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var listedPaths = manifest.Artifacts
            .Select(artifact => Path.GetFullPath(
                Path.Combine(
                    rootPath,
                    artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToHashSet(pathComparison);
        var normalizedManifestPath = Path.GetFullPath(manifestPath);

        foreach (var discoveredPath in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var resolvedPath = PathSafety.EnsureStrictDescendant(
                rootPath,
                new FileInfo(discoveredPath),
                mustExist: true);

            if (pathComparison.Equals(resolvedPath, normalizedManifestPath))
            {
                continue;
            }

            if (!listedPaths.Contains(resolvedPath))
            {
                throw new InvalidDataException(
                    $"Artifact root contains unlisted file '{Path.GetRelativePath(rootPath, resolvedPath)}'.");
            }
        }
    }

    private static void ValidateResultRelations(
        PipelineArtifactManifest manifest)
    {
        var failureLogPaths = manifest.Artifacts
            .Where(artifact => artifact.Kind == PipelineArtifactKind.FailureLog)
            .Select(artifact => artifact.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var testArtifactPaths = manifest.Artifacts
            .Where(artifact => artifact.Kind == PipelineArtifactKind.TestResult)
            .Select(artifact => artifact.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referencedTestPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var targetNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var target in manifest.Targets)
        {
            ValidateText(target.Name, "target name");
            ValidateText(target.Configuration, "target configuration");

            var unexpectedSuccessLog =
                target.Succeeded && target.FailureLogPath is not null;
            var missingFailureLog =
                !target.Succeeded
                && (target.FailureLogPath is null
                    || !failureLogPaths.Contains(target.FailureLogPath));

            if (!targetNames.Add(target.Name)
                || unexpectedSuccessLog
                || missingFailureLog)
            {
                throw new InvalidDataException(
                    $"Target result '{target.Name}' is inconsistent.");
            }
        }

        var testNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var test in manifest.Tests)
        {
            ValidateText(test.Name, "test name");
            ValidateText(test.Configuration, "test configuration");

            var invalidSuccessfulCounts =
                test.Succeeded
                && (test.Total == 0 || test.Failed != 0);
            var invalidResultPath = test.ResultPaths?.Any(path =>
                path.Contains('\\', StringComparison.Ordinal)
                || !testArtifactPaths.Contains(path)
                || !referencedTestPaths.Add(path)) != false;

            if (!testNames.Add(test.Name)
                || !Enum.IsDefined(test.Command)
                || test.Total < 0
                || test.Passed < 0
                || test.Failed < 0
                || test.Skipped < 0
                || test.Total != test.Passed + test.Failed + test.Skipped
                || invalidSuccessfulCounts
                || test.ResultPaths is null
                || invalidResultPath)
            {
                throw new InvalidDataException(
                    $"Test result '{test.Name}' is inconsistent with its artifacts.");
            }
        }

        foreach (var failure in manifest.Failures)
        {
            ValidateText(failure.Code, "failure code");
            ValidateText(failure.Stage, "failure stage");
            ValidateText(failure.Summary, "failure summary");
        }

        if (manifest.Status == PipelineRunStatus.Succeeded
            && (manifest.Targets.Any(target => !target.Succeeded)
                || manifest.Tests.Any(test => !test.Succeeded)))
        {
            throw new InvalidDataException(
                "A successful manifest cannot contain failed target results.");
        }
    }

    private static PipelineArtifactManifest Normalize(
        PipelineArtifactManifest manifest)
    {
        return manifest with
        {
            Artifacts = manifest.Artifacts
                .Select(artifact => artifact with
                {
                    RelativePath = artifact.RelativePath.Replace('\\', '/'),
                })
                .OrderBy(artifact => artifact.Kind)
                .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
                .ToArray(),
            Failures = manifest.Failures
                .OrderBy(failure => failure.Code, StringComparer.Ordinal)
                .ThenBy(failure => failure.Stage, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static (string Id, string Version) ReadPackageIdentity(
        string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspecEntries = archive.Entries
            .Where(entry => entry.FullName.EndsWith(
                ".nuspec",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (nuspecEntries.Length != 1)
        {
            throw new InvalidDataException(
                $"Package '{packagePath}' must contain exactly one nuspec.");
        }

        using var stream = nuspecEntries[0].Open();
        var document = XDocument.Load(stream);
        var metadata = document.Root?.Elements()
            .SingleOrDefault(element => element.Name.LocalName == "metadata")
            ?? throw new InvalidDataException("Package nuspec metadata is missing.");
        var id = metadata.Elements()
            .Single(element => element.Name.LocalName == "id")
            .Value;
        var version = metadata.Elements()
            .Single(element => element.Name.LocalName == "version")
            .Value;
        return (id, NormalizePackageVersion(version));
    }

    private static void ValidatePackageMetadata(
        PipelineArtifact artifact,
        string relativePath)
    {
        var invalidPrimaryExtension =
            artifact.Kind == PipelineArtifactKind.NuGetPackage
            && !relativePath.EndsWith(
                ".nupkg",
                StringComparison.OrdinalIgnoreCase);
        var invalidSymbolExtension =
            artifact.Kind == PipelineArtifactKind.SymbolPackage
            && !relativePath.EndsWith(
                ".snupkg",
                StringComparison.OrdinalIgnoreCase);

        var isValid = !string.IsNullOrWhiteSpace(artifact.PackageId)
            && !artifact.PackageId.Any(char.IsControl)
            && artifact.PackageVersion is not null
            && string.Equals(
                artifact.PackageVersion,
                NormalizePackageVersion(artifact.PackageVersion),
                StringComparison.Ordinal)
            && artifact.TargetFramework is null
            && artifact.RuntimeIdentifier is null
            && !invalidPrimaryExtension
            && !invalidSymbolExtension;

        if (isValid)
        {
            return;
        }

        throw new InvalidDataException(
            $"Package artifact '{relativePath}' has invalid metadata.");
    }

    private static void ValidateKindMetadata(
        PipelineArtifact artifact,
        string artifactPath)
    {
        switch (artifact.Kind)
        {
            case PipelineArtifactKind.PublishArchive:
                ValidatePublishArchive(artifact, artifactPath);
                return;

            case PipelineArtifactKind.FailureLog:
                ValidateFailureLog(artifact, artifactPath);
                return;

            case PipelineArtifactKind.TestResult:
                ValidateTestArtifactMetadata(artifact);
                return;

            default:
                return;
        }
    }

    private static void ValidatePublishArchive(
        PipelineArtifact artifact,
        string artifactPath)
    {
        if (!artifact.RelativePath.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Publish artifact '{artifact.RelativePath}' is not a ZIP archive.");
        }

        ValidateArchive(artifactPath);
    }

    private static void ValidateFailureLog(
        PipelineArtifact artifact,
        string artifactPath)
    {
        if (!artifact.RelativePath.EndsWith(
                    ".failure.txt",
                    StringComparison.OrdinalIgnoreCase)
            || artifact.Size > 64 * 1024)
        {
            throw new InvalidDataException(
                $"Failure log '{artifact.RelativePath}' is invalid.");
        }

        string content;

        try
        {
            content = new UTF8Encoding(false, true)
                .GetString(File.ReadAllBytes(artifactPath));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Failure log '{artifact.RelativePath}' is not UTF-8.",
                exception);
        }

        var lines = content.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
        var expectedPrefixes = new[]
        {
            "stage=",
            "commandKind=",
            "exitCode=",
            "timedOut=",
            "cancelled=",
            "summary=",
        };
        var validShape = lines.Length == expectedPrefixes.Length
                         && lines.Zip(expectedPrefixes)
                             .All(pair => pair.First.StartsWith(
                                 pair.Second,
                                 StringComparison.Ordinal))
                         && string.Equals(
                             lines[0]["stage=".Length..],
                             artifact.LogicalName,
                             StringComparison.Ordinal)
                         && new[] { "clean", "build", "test", "pack", "publish", }
                             .Contains(
                                 lines[1]["commandKind=".Length..],
                                 StringComparer.Ordinal)
                         && int.TryParse(
                             lines[2]["exitCode=".Length..],
                             System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture,
                             out _)
                         && bool.TryParse(
                             lines[3]["timedOut=".Length..],
                             out _)
                         && bool.TryParse(
                             lines[4]["cancelled=".Length..],
                             out _)
                         && string.Equals(
                             lines[5],
                             "summary=The configured command did not succeed.",
                             StringComparison.Ordinal);

        if (validShape)
        {
            return;
        }

        throw new InvalidDataException(
            $"Failure log '{artifact.RelativePath}' has unsafe content.");
    }

    private static void ValidateTestArtifactMetadata(
        PipelineArtifact artifact)
    {
        if (artifact.TargetFramework is null
            && artifact.RuntimeIdentifier is null)
        {
            return;
        }

        throw new InvalidDataException(
            $"Artifact '{artifact.RelativePath}' has contradictory metadata.");
    }

    private static void ValidateArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entryNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var fileEntries = archive.Entries
            .Where(entry => !entry.FullName.EndsWith('/'))
            .ToArray();

        if (fileEntries.Length == 0)
        {
            throw new InvalidDataException(
                $"Publish archive '{archivePath}' is empty.");
        }

        var sortedNames = fileEntries
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (!fileEntries.Select(entry => entry.FullName)
            .SequenceEqual(sortedNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Publish archive '{archivePath}' entries are not sorted.");
        }

        foreach (var entry in fileEntries)
        {
            var segments = entry.FullName
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;

            if (entry.FullName.Contains('\\', StringComparison.Ordinal)
                || entry.FullName.StartsWith('/')
                || segments.Length == 0
                || segments.Any(segment => segment is "." or "..")
                || !entryNames.Add(entry.FullName)
                || unixMode == 0xA000)
            {
                throw new InvalidDataException(
                    $"Publish archive '{archivePath}' contains an unsafe entry.");
            }
        }
    }

    private static string NormalizePackageVersion(string version)
    {
        try
        {
            return NuGetVersion.Parse(version).ToNormalizedString();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Package version '{version}' is invalid.",
                exception);
        }
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64
               && value.All(character => character is >= '0' and <= '9'
                   or >= 'a' and <= 'f');
    }

    private static async Task<string?> TryReadGitRevisionAsync(
        string consumerRootPath,
        CancellationToken cancellationToken)
    {
        using var process = new Process()
        {
            StartInfo = new("git")
            {
                WorkingDirectory = consumerRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("rev-parse");
        process.StartInfo.ArgumentList.Add("--verify");
        process.StartInfo.ArgumentList.Add("HEAD");

        try
        {
            if (!process.Start())
            {
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var outputTask = process.StandardOutput.ReadToEndAsync(
                timeout.Token);
            await process.WaitForExitAsync(timeout.Token)
                .ConfigureAwait(false);
            var output = (await outputTask.ConfigureAwait(false)).Trim();
            return process.ExitCode == 0
                   && !string.IsNullOrWhiteSpace(output)
                   && !output.Any(char.IsControl)
                ? output
                : null;
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void ValidateManifestFileName(string manifestFileName)
    {
        if (!string.IsNullOrWhiteSpace(manifestFileName)
            && string.Equals(
                manifestFileName,
                Path.GetFileName(manifestFileName),
                StringComparison.Ordinal)
            && manifestFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
        {
            return;
        }

        throw new InvalidDataException(
            "The manifest filename must be one safe root-level filename.");
    }

    private static void ValidateText(string value, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsControl))
        {
            return;
        }

        throw new InvalidDataException(
            $"The manifest {fieldName} is invalid.");
    }
}