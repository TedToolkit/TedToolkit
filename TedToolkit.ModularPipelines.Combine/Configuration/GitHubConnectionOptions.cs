// -----------------------------------------------------------------------
// <copyright file="GitHubConnectionOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains GitHub or GitHub Enterprise connection settings.
/// </summary>
public sealed record GitHubConnectionOptions
{
    /// <summary>
    /// Gets the web instance URL.
    /// </summary>
    public Uri? InstanceUrl { get; init; }

    /// <summary>
    /// Gets the API base URL.
    /// </summary>
    public Uri? ApiUrl { get; init; }

    /// <summary>
    /// Gets the repository owner.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>
    /// Gets the authentication mode.
    /// </summary>
    public GitHubAuthenticationMode AuthenticationMode { get; init; } =
        GitHubAuthenticationMode.ActionsToken;

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