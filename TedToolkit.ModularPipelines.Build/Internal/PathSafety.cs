// -----------------------------------------------------------------------
// <copyright file="PathSafety.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Internal;

/// <summary>
/// Provides root containment and link/reparse-point validation.
/// </summary>
internal static class PathSafety
{
    /// <summary>
    /// Determines whether a candidate is a strict descendant of a parent.
    /// </summary>
    /// <param name="parentPath">The absolute parent path.</param>
    /// <param name="candidatePath">The absolute candidate path.</param>
    /// <returns><see langword="true"/> when the candidate is inside the parent.</returns>
    public static bool IsWithin(string parentPath, string candidatePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedParent = TrimEndingSeparator(Path.GetFullPath(parentPath));
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(
            $"{normalizedParent}{Path.DirectorySeparatorChar}",
            comparison);
    }

    /// <summary>
    /// Validates and normalizes an existing non-link root.
    /// </summary>
    /// <param name="root">The explicit root.</param>
    /// <returns>The normalized absolute root path.</returns>
    /// <exception cref="DirectoryNotFoundException"><paramref name="root"/> does not exist.</exception>
    /// <exception cref="InvalidDataException"><paramref name="root"/> is not absolute or crosses a link.</exception>
    public static string EnsureExistingRoot(DirectoryInfo root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!Path.IsPathFullyQualified(root.ToString()))
        {
            throw new InvalidDataException(
                "The explicit root path must be absolute.");
        }

        var fullPath = Path.GetFullPath(root.FullName);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Root directory '{fullPath}' does not exist.");
        }

        EnsureNoReparsePoints(fullPath, fullPath);
        return TrimEndingSeparator(fullPath);
    }

    /// <summary>
    /// Validates a strict descendant without crossing an existing link or reparse point.
    /// </summary>
    /// <param name="rootPath">The validated absolute root.</param>
    /// <param name="candidate">The candidate file or directory.</param>
    /// <param name="mustExist">Whether the complete candidate must exist.</param>
    /// <returns>The normalized absolute candidate path.</returns>
    /// <exception cref="InvalidDataException">The candidate escapes the root or crosses a link.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="mustExist"/> is true and the candidate does not exist.</exception>
    public static string EnsureStrictDescendant(
        string rootPath,
        FileSystemInfo candidate,
        bool mustExist)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!Path.IsPathFullyQualified(candidate.ToString()))
        {
            throw new InvalidDataException(
                "Explicit file and directory paths must be absolute.");
        }

        var fullPath = Path.GetFullPath(candidate.FullName);

        if (!IsStrictDescendant(rootPath, fullPath))
        {
            throw new InvalidDataException(
                $"Path '{fullPath}' must be a strict descendant of '{rootPath}'.");
        }

        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Required path '{fullPath}' does not exist.",
                fullPath);
        }

        EnsureNoReparsePoints(rootPath, fullPath);
        return fullPath;
    }

    /// <summary>
    /// Resolves a portable relative artifact file under a validated root.
    /// </summary>
    /// <param name="rootPath">The validated artifact root.</param>
    /// <param name="relativePath">The portable relative path.</param>
    /// <param name="mustExist">Whether the file must exist.</param>
    /// <returns>The normalized absolute file path.</returns>
    /// <exception cref="InvalidDataException">The relative path is unsafe or escapes through a link.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="mustExist"/> is true and the file does not exist.</exception>
    public static string ResolveRelativeFile(
        string rootPath,
        string relativePath,
        bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Artifact path '{relativePath}' is invalid.");
        }

        var normalizedSegments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (normalizedSegments.Length == 0
            || normalizedSegments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException(
                $"Artifact path '{relativePath}' contains traversal.");
        }

        var fullPath = Path.GetFullPath(
            Path.Combine(rootPath, Path.Combine(normalizedSegments)));

        if (!IsStrictDescendant(rootPath, fullPath))
        {
            throw new InvalidDataException(
                $"Artifact path '{relativePath}' escapes its root.");
        }

        if (mustExist && !File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Artifact '{relativePath}' does not exist.",
                fullPath);
        }

        EnsureNoReparsePoints(rootPath, fullPath);
        return fullPath;
    }

    /// <summary>
    /// Determines whether a normalized candidate is a strict descendant.
    /// </summary>
    /// <param name="rootPath">The normalized root.</param>
    /// <param name="candidatePath">The normalized candidate.</param>
    /// <returns><see langword="true"/> when the candidate is a strict descendant.</returns>
    public static bool IsStrictDescendant(string rootPath, string candidatePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootWithSeparator = Path.EndsInDirectorySeparator(rootPath)
            ? rootPath
            : $"{rootPath}{Path.DirectorySeparatorChar}";

        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }

    private static void EnsureNoReparsePoints(
        string rootPath,
        string candidatePath)
    {
        var current = new DirectoryInfo(rootPath);

        if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"Root '{rootPath}' must not be a link or reparse point.");
        }

        var relative = Path.GetRelativePath(rootPath, candidatePath);
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,],
            StringSplitOptions.RemoveEmptyEntries);
        var inspectedPath = rootPath;

        foreach (var segment in segments)
        {
            inspectedPath = Path.Combine(inspectedPath, segment);

            if (!File.Exists(inspectedPath) && !Directory.Exists(inspectedPath))
            {
                continue;
            }

            if ((File.GetAttributes(inspectedPath)
                 & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"Path '{candidatePath}' crosses link or reparse point '{inspectedPath}'.");
            }
        }
    }

    private static string TrimEndingSeparator(string path)
    {
        return Path.TrimEndingDirectorySeparator(path);
    }
}