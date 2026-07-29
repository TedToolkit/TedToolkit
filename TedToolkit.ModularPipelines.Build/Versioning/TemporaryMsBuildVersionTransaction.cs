// -----------------------------------------------------------------------
// <copyright file="TemporaryMsBuildVersionTransaction.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

using TedToolkit.ModularPipelines.Build.Internal;

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents the internal single-writer temporary MSBuild version transaction.
/// </summary>
internal sealed class TemporaryMsBuildVersionTransaction : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
    };

    private readonly string _targetPath;

    private readonly string _recoveryPath;

    private readonly RecoveryRecord _record;

    private FileStream? _lock;

    private bool _restored;

    private TemporaryMsBuildVersionTransaction(
        string targetPath,
        string recoveryPath,
        RecoveryRecord record,
        FileStream recoveryLock)
    {
        _targetPath = targetPath;
        _recoveryPath = recoveryPath;
        _record = record;
        _lock = recoveryLock;
    }

    /// <summary>
    /// Recovers a matching interrupted transaction without overwriting conflicting bytes.
    /// </summary>
    /// <param name="rootDirectory">The validated repository root.</param>
    /// <param name="artifactRootDirectory">The strict-descendant artifact root.</param>
    /// <param name="options">The configured target and recovery paths.</param>
    /// <param name="cancellationToken">A token that cancels recovery I/O.</param>
    /// <returns>A task that completes after recovery or confirms no recovery is needed.</returns>
    /// <exception cref="InvalidDataException">Recovery metadata or current target bytes conflict with the configured transaction.</exception>
    public static async Task RecoverAsync(
        DirectoryInfo rootDirectory,
        DirectoryInfo artifactRootDirectory,
        TemporaryMsBuildVersionOptions options,
        CancellationToken cancellationToken)
    {
        var paths = ValidatePaths(
            rootDirectory,
            artifactRootDirectory,
            options,
            requireTargetVersion: false);

        if (!File.Exists(paths.RecoveryPath))
        {
            return;
        }

        var recoveryLock = new FileStream(
            paths.RecoveryPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var recoveryLockLease =
            recoveryLock.ConfigureAwait(false);
        var record = await JsonSerializer.DeserializeAsync<RecoveryRecord>(
                recoveryLock,
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("The version recovery file is empty.");
        ValidateRecord(paths, options, record);
        var currentBytes = await File.ReadAllBytesAsync(
                paths.TargetPath,
                cancellationToken)
            .ConfigureAwait(false);
        var currentHash = Hash(currentBytes);

        if (string.Equals(
                currentHash,
                record.OriginalSha256,
                StringComparison.Ordinal))
        {
            recoveryLock.Close();
            File.Delete(paths.RecoveryPath);
            return;
        }

        if (!string.Equals(
                currentHash,
                record.TemporarySha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The MSBuild version target no longer matches the recorded original or temporary content.");
        }

        byte[] originalBytes;

        try
        {
            originalBytes = Convert.FromBase64String(record.OriginalBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "The MSBuild version recovery content is not valid base64.",
                exception);
        }

        if (!string.Equals(
                Hash(originalBytes),
                record.OriginalSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The MSBuild version recovery content does not match its hash.");
        }

        await AtomicWriteAsync(
                paths.TargetPath,
                originalBytes,
                cancellationToken)
            .ConfigureAwait(false);
        recoveryLock.Close();
        File.Delete(paths.RecoveryPath);
    }

    /// <summary>
    /// Begins a new single-writer version transaction and applies temporary bytes.
    /// </summary>
    /// <param name="rootDirectory">The validated repository root.</param>
    /// <param name="artifactRootDirectory">The strict-descendant artifact root.</param>
    /// <param name="options">The target version and recovery paths.</param>
    /// <returns>The active transaction, which must be restored or asynchronously disposed.</returns>
    /// <exception cref="InvalidDataException">The target, version, or existing recovery state is invalid.</exception>
    public static async Task<TemporaryMsBuildVersionTransaction> BeginAsync(
        DirectoryInfo rootDirectory,
        DirectoryInfo artifactRootDirectory,
        TemporaryMsBuildVersionOptions options)
    {
        var paths = ValidatePaths(
            rootDirectory,
            artifactRootDirectory,
            options,
            requireTargetVersion: true);

        if (File.Exists(paths.RecoveryPath))
        {
            throw new InvalidDataException(
                "A version recovery file already exists. Recover it before starting a new transaction.");
        }

        var originalBytes = await File.ReadAllBytesAsync(paths.TargetPath)
            .ConfigureAwait(false);
        var temporaryBytes = CreateTemporaryBytes(
            originalBytes,
            options.PropertyName,
            options.TargetVersion!);
        var record = new RecoveryRecord()
        {
            RelativeTargetPath = Path.GetRelativePath(
                    paths.RootPath,
                    paths.TargetPath)
                .Replace('\\', '/'),
            PropertyName = options.PropertyName,
            TargetVersion = options.TargetVersion!,
            OriginalBase64 = Convert.ToBase64String(originalBytes),
            OriginalSha256 = Hash(originalBytes),
            TemporarySha256 = Hash(temporaryBytes),
        };
        var recoveryBytes = JsonSerializer.SerializeToUtf8Bytes(
            record,
            SerializerOptions);
        var temporaryRecoveryPath = Path.Combine(
            Path.GetDirectoryName(paths.RecoveryPath)!,
            $".{Path.GetFileName(paths.RecoveryPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var stream = new FileStream(
                             temporaryRecoveryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(recoveryBytes)
                    .ConfigureAwait(false);
                await stream.FlushAsync()
                    .ConfigureAwait(false);
            }

            File.Move(
                temporaryRecoveryPath,
                paths.RecoveryPath,
                overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryRecoveryPath))
            {
                File.Delete(temporaryRecoveryPath);
            }
        }

        FileStream? recoveryLock = null;

        try
        {
            recoveryLock = new(
                paths.RecoveryPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            await AtomicWriteAsync(
                    paths.TargetPath,
                    temporaryBytes,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return new TemporaryMsBuildVersionTransaction(
                paths.TargetPath,
                paths.RecoveryPath,
                record,
                recoveryLock);
        }
        catch
        {
            if (recoveryLock is not null)
            {
                await recoveryLock.DisposeAsync()
                    .ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// Restores the exact original target bytes and deletes recovery state.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels restoration I/O.</param>
    /// <returns>A task that completes after exact restoration.</returns>
    /// <exception cref="InvalidDataException">The active target no longer matches the recorded temporary bytes.</exception>
    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (_restored)
        {
            return;
        }

        try
        {
            var currentBytes = await File.ReadAllBytesAsync(
                    _targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(
                    Hash(currentBytes),
                    _record.TemporarySha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The temporary MSBuild version file changed during the transaction.");
            }

            var originalBytes = Convert.FromBase64String(
                _record.OriginalBase64);
            await AtomicWriteAsync(
                    _targetPath,
                    originalBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            var restoredBytes = await File.ReadAllBytesAsync(
                    _targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(
                    Hash(restoredBytes),
                    _record.OriginalSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The restored MSBuild version target does not match its original hash.");
            }

            await ReleaseLockAsync()
                .ConfigureAwait(false);
            File.Delete(_recoveryPath);
            _restored = true;
        }
        catch
        {
            await ReleaseLockAsync()
                .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Releases the test transaction lock without restoring to simulate process termination.
    /// </summary>
    /// <returns>A task that completes after releasing the lock.</returns>
    public async Task AbandonForTestAsync()
    {
        await ReleaseLockAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Restores an active transaction during asynchronous disposal.
    /// </summary>
    /// <returns>A value task that completes after restoration.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_restored && _lock is not null)
        {
            await RestoreAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static ValidatedPaths ValidatePaths(
        DirectoryInfo rootDirectory,
        DirectoryInfo artifactRootDirectory,
        TemporaryMsBuildVersionOptions options,
        bool requireTargetVersion)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rootPath = PathSafety.EnsureExistingRoot(rootDirectory);
        var artifactPath = PathSafety.EnsureStrictDescendant(
            rootPath,
            artifactRootDirectory,
            mustExist: false);
        var targetPath = PathSafety.EnsureStrictDescendant(
            rootPath,
            options.TargetFile,
            mustExist: true);
        var recoveryPath = PathSafety.EnsureStrictDescendant(
            rootPath,
            options.RecoveryFile,
            mustExist: false);

        if (File.Exists(artifactPath)
            || !File.Exists(targetPath)
            || string.Equals(
                targetPath,
                recoveryPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)
            || PathSafety.IsWithin(artifactPath, recoveryPath))
        {
            throw new InvalidDataException(
                "The version target/recovery paths are invalid.");
        }

        try
        {
            _ = System.Xml.XmlConvert.VerifyNCName(options.PropertyName);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or System.Xml.XmlException)
        {
            throw new InvalidDataException(
                "The MSBuild version property name is invalid.",
                exception);
        }

        if (requireTargetVersion)
        {
            _ = DailyReleaseVersionPolicy.Parse(options.TargetVersion!);
        }

        return new(
            rootPath,
            artifactPath,
            targetPath,
            recoveryPath);
    }

    private static byte[] CreateTemporaryBytes(
        byte[] originalBytes,
        string propertyName,
        string targetVersion)
    {
        XDocument document;

        try
        {
            using var input = new MemoryStream(originalBytes);
            document = XDocument.Load(input, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (
            exception is System.Xml.XmlException
            or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The MSBuild version target is not valid XML.",
                exception);
        }

        var properties = document
            .Descendants()
            .Where(element => string.Equals(
                element.Name.LocalName,
                propertyName,
                StringComparison.Ordinal))
            .ToArray();

        if (properties.Length != 1)
        {
            throw new InvalidDataException(
                $"The MSBuild version target must contain exactly one '{propertyName}' property.");
        }

        properties[0].Value = targetVersion;
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(
                   output,
                   new UTF8Encoding(
                       encoderShouldEmitUTF8Identifier: originalBytes.AsSpan()
                           .StartsWith(Encoding.UTF8.Preamble),
                       throwOnInvalidBytes: true),
                   leaveOpen: true))
        {
            document.Save(writer, SaveOptions.DisableFormatting);
        }

        return output.ToArray();
    }

    private static async Task AtomicWriteAsync(
        string targetPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(
                    temporaryPath,
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateRecord(
        ValidatedPaths paths,
        TemporaryMsBuildVersionOptions options,
        RecoveryRecord record)
    {
        var expectedRelativePath = Path.GetRelativePath(
                paths.RootPath,
                paths.TargetPath)
            .Replace('\\', '/');

        if (string.Equals(
                record.RelativeTargetPath,
                expectedRelativePath,
                StringComparison.Ordinal)
            && string.Equals(
                record.PropertyName,
                options.PropertyName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(record.OriginalBase64)
            && IsSha256(record.OriginalSha256)
            && IsSha256(record.TemporarySha256))
        {
            _ = DailyReleaseVersionPolicy.Parse(record.TargetVersion);
            return;
        }

        throw new InvalidDataException(
            "The version recovery file does not match the configured target.");
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64
               && value.All(character => character is >= '0' and <= '9'
                   or >= 'a' and <= 'f');
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private async Task ReleaseLockAsync()
    {
        if (_lock is null)
        {
            return;
        }

        await _lock.DisposeAsync()
            .ConfigureAwait(false);
        _lock = null;
    }

    private sealed record RecoveryRecord
    {
        public required string RelativeTargetPath { get; init; }

        public required string PropertyName { get; init; }

        public required string TargetVersion { get; init; }

        public required string OriginalBase64 { get; init; }

        public required string OriginalSha256 { get; init; }

        public required string TemporarySha256 { get; init; }
    }

    private sealed record ValidatedPaths(
        string RootPath,
        string ArtifactPath,
        string TargetPath,
        string RecoveryPath);
}