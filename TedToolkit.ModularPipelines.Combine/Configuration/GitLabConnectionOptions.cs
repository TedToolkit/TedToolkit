// -----------------------------------------------------------------------
// <copyright file="GitLabConnectionOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains GitLab or self-managed GitLab connection settings.
/// </summary>
public sealed record GitLabConnectionOptions
{
    /// <summary>
    /// Gets the web instance URL.
    /// </summary>
    public Uri? InstanceUrl { get; init; }

    /// <summary>
    /// Gets the API base URL, including any proxy path prefix.
    /// </summary>
    public Uri? ApiUrl { get; init; }

    /// <summary>
    /// Gets the numeric GitLab project identifier.
    /// </summary>
    public long? ProjectId { get; init; }

    /// <summary>
    /// Gets the authentication mode.
    /// </summary>
    public GitLabAuthenticationMode AuthenticationMode { get; init; }

    /// <summary>
    /// Gets the logical token reference.
    /// </summary>
    public string? CredentialReference { get; init; }

    /// <summary>
    /// Gets whether explicit HTTP URLs are accepted.
    /// </summary>
    public bool AllowInsecureHttp { get; init; }

    /// <summary>
    /// Gets the timeout for each provider request.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(2);
}