// -----------------------------------------------------------------------
// <copyright file="ProviderUri.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Constructs provider URLs while preserving configured path prefixes.
/// </summary>
internal static class ProviderUri
{
    /// <summary>
    /// Appends escaped dynamic path segments.
    /// </summary>
    /// <param name="baseUri">The validated absolute base.</param>
    /// <param name="segments">Unescaped path segments.</param>
    /// <returns>The constructed absolute URI.</returns>
    public static Uri Append(Uri baseUri, params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(segments);
        var suffix = string.Join(
            '/',
            segments.Select(Uri.EscapeDataString));
        return new(
            $"{baseUri.AbsoluteUri.TrimEnd('/')}/{suffix}",
            UriKind.Absolute);
    }

    /// <summary>
    /// Adds a query string to an already constructed provider URI.
    /// </summary>
    /// <param name="uri">The URI without a query.</param>
    /// <param name="values">Unescaped query keys and values.</param>
    /// <returns>The URI containing the encoded query.</returns>
    public static Uri WithQuery(
        Uri uri,
        params (string Key, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var query = string.Join(
            '&',
            values.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new($"{uri.AbsoluteUri}?{query}", UriKind.Absolute);
    }
}