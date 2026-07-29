// -----------------------------------------------------------------------
// <copyright file="GitHubActionsEnvironment.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Reads the small GitHub event fields that are not exposed as environment
/// variables.
/// </summary>
internal static class GitHubActionsEnvironment
{
    /// <summary>
    /// Reads the push event's before revision without performing a network call.
    /// </summary>
    /// <returns>The raw before revision, when safely available.</returns>
    public static string? ReadBeforeRevision()
    {
        var explicitValue = Environment.GetEnvironmentVariable(
            "GITHUB_EVENT_BEFORE");

        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }

        var path = Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var file = new FileInfo(path);

        if (!file.Exists
            || file.LinkTarget is not null
            || file.Length is <= 0 or > 1024 * 1024)
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty(
                    "before",
                    out var before)
                && before.ValueKind == JsonValueKind.String
                ? before.GetString()
                : null;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            return null;
        }
    }
}