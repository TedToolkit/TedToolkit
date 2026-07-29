// -----------------------------------------------------------------------
// <copyright file="IssueClosingDirectiveMerger.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Retains provider-neutral issue-closing lines during request updates.
/// </summary>
internal static class IssueClosingDirectiveMerger
{
    private static readonly string[] Keywords =
    [
        "close",
        "closes",
        "closed",
        "fix",
        "fixes",
        "fixed",
        "resolve",
        "resolves",
        "resolved",
    ];

    /// <summary>
    /// Merges recognized lines from an existing body into a replacement body.
    /// </summary>
    /// <param name="replacement">The normalized generated replacement.</param>
    /// <param name="existing">The provider's existing request body.</param>
    /// <param name="preserve">Whether recognized directives are retained.</param>
    /// <returns>The deterministic merged body.</returns>
    public static string Merge(
        string replacement,
        string existing,
        bool preserve)
    {
        var normalized = replacement.ReplaceLineEndings("\n").TrimEnd();

        if (!preserve)
        {
            return normalized;
        }

        var known = normalized.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retained = existing.ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(IsDirective)
            .Where(known.Add)
            .ToArray();

        if (retained.Length == 0)
        {
            return normalized;
        }

        return normalized.Length == 0
            ? string.Join('\n', retained)
            : $"{normalized}\n\n{string.Join('\n', retained)}";
    }

    private static bool IsDirective(string line)
    {
        var separator = line.IndexOf(' ', StringComparison.Ordinal);

        if (separator <= 0
            || !Keywords.Contains(
                line[..separator],
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var references = line[(separator + 1)..].Split(
            [',', ' ',],
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        return references.Length > 0
               && references.All(IsIssueReference);
    }

    private static bool IsIssueReference(string value)
    {
        var marker = value.LastIndexOf('#');
        return marker >= 0
               && marker < value.Length - 1
               && value[(marker + 1)..].All(char.IsAsciiDigit)
               && (marker == 0
                   || IsRepositoryPath(value[..marker]));
    }

    private static bool IsRepositoryPath(string value)
    {
        var separator = value.IndexOf('/', StringComparison.Ordinal);
        return separator > 0
               && separator < value.Length - 1
               && value.All(character =>
                   char.IsAsciiLetterOrDigit(character)
                   || character is '.' or '_' or '-' or '/');
    }
}