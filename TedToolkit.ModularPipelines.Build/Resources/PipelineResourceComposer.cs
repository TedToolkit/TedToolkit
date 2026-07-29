// -----------------------------------------------------------------------
// <copyright file="PipelineResourceComposer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using TedToolkit.ModularPipelines.Build.Internal;

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents the standard strict UTF-8 implementation of resource composition.
/// </summary>
public sealed class PipelineResourceComposer : IPipelineResourceComposer
{
    private const int MaximumResourceBytes = 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <inheritdoc/>
    public Task<PipelineResourceDocument> GetEditorConfigAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ComposeAsync(
            rootDirectory,
            StandardResources.EditorConfigBase,
            options.EditorConfigOverlayPath,
            options.EditorConfigReplacementPath,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PipelineResourceDocument> GetCommitMessageInstructionsAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ComposeAsync(
            rootDirectory,
            StandardResources.CommitMessagePromptBase,
            options.CommitMessageOverlayPath,
            options.CommitMessageReplacementPath,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PipelineResourceDocument> GetChangeRequestInstructionsAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ComposeAsync(
            rootDirectory,
            StandardResources.ChangeRequestPromptBase,
            options.ChangeRequestOverlayPath,
            options.ChangeRequestReplacementPath,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PipelineResourceWriteResult> WriteEditorConfigAsync(
        DirectoryInfo rootDirectory,
        EmbeddedResourceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rootPath = PathSafety.EnsureExistingRoot(rootDirectory);
        var document = await GetEditorConfigAsync(
                rootDirectory,
                options,
                cancellationToken)
            .ConfigureAwait(false);
        var targetPath = Path.Combine(rootPath, ".editorconfig");
        var target = new FileInfo(targetPath);
        var bytes = StrictUtf8.GetBytes(document.Content);

        if (!options.WriteEditorConfig)
        {
            return new(document, target, false);
        }

        if (target.Exists)
        {
            PathSafety.EnsureStrictDescendant(
                rootPath,
                target,
                mustExist: true);
            var existing = await File.ReadAllBytesAsync(
                    targetPath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing.AsSpan().SequenceEqual(bytes))
            {
                return new(document, target, false);
            }
        }

        var temporaryPath = Path.Combine(
            rootPath,
            $".editorconfig.{Guid.NewGuid():N}.tmp");

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

        target.Refresh();
        return new(document, target, true);
    }

    private static async Task<PipelineResourceDocument> ComposeAsync(
        DirectoryInfo rootDirectory,
        string embeddedBase,
        string? overlayPath,
        string? replacementPath,
        CancellationToken cancellationToken)
    {
        var rootPath = PathSafety.EnsureExistingRoot(rootDirectory);

        if (overlayPath is not null && replacementPath is not null)
        {
            throw new InvalidDataException(
                "A resource cannot specify both an overlay and a replacement.");
        }

        string content;
        PipelineResourceSource source;

        if (replacementPath is not null)
        {
            content = await ReadOverrideAsync(
                    rootPath,
                    replacementPath,
                    cancellationToken)
                .ConfigureAwait(false);
            source = PipelineResourceSource.Replacement;
        }
        else if (overlayPath is not null)
        {
            var overlay = await ReadOverrideAsync(
                    rootPath,
                    overlayPath,
                    cancellationToken)
                .ConfigureAwait(false);
            content =
                $"{Normalize(embeddedBase).TrimEnd()}\n\n{Normalize(overlay).Trim()}\n";
            source = PipelineResourceSource.EmbeddedBaseWithOverlay;
        }
        else
        {
            content = embeddedBase;
            source = PipelineResourceSource.EmbeddedBase;
        }

        content = Normalize(content);
        var contentBytes = StrictUtf8.GetBytes(content);

        if (contentBytes.Length > MaximumResourceBytes)
        {
            throw new InvalidDataException(
                "The composed resource exceeds the 1 MiB limit.");
        }

        return new(
            content,
            StandardResources.ResourceVersion,
            Convert.ToHexStringLower(SHA256.HashData(contentBytes)),
            source);
    }

    private static async Task<string> ReadOverrideAsync(
        string rootPath,
        string configuredPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidDataException("A resource path cannot be empty.");
        }

        var candidate = Path.IsPathFullyQualified(configuredPath)
            ? configuredPath
            : Path.Combine(rootPath, configuredPath);
        var resolved = PathSafety.EnsureStrictDescendant(
            rootPath,
            new FileInfo(candidate),
            mustExist: true);
        var file = new FileInfo(resolved);

        if (file.Length > MaximumResourceBytes)
        {
            throw new InvalidDataException(
                $"Resource override '{configuredPath}' exceeds the 1 MiB limit.");
        }

        var bytes = await File.ReadAllBytesAsync(resolved, cancellationToken)
            .ConfigureAwait(false);
        string content;

        try
        {
            content = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Resource override '{configuredPath}' is not valid UTF-8.",
                exception);
        }

        if (content.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Resource override '{configuredPath}' contains a NUL character.");
        }

        return content;
    }

    private static string Normalize(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n');
        return $"{normalized}\n";
    }
}