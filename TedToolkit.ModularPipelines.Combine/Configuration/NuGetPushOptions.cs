// -----------------------------------------------------------------------
// <copyright file="NuGetPushOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains explicit NuGet push configuration.
/// </summary>
public sealed record NuGetPushOptions
{
    /// <summary>
    /// Gets the primary package source.
    /// </summary>
    public Uri? Source { get; init; }

    /// <summary>
    /// Gets an optional symbol source.
    /// </summary>
    public Uri? SymbolSource { get; init; }

    /// <summary>
    /// Gets the authentication mode.
    /// </summary>
    public NuGetAuthenticationMode AuthenticationMode { get; init; } =
        NuGetAuthenticationMode.ApiKey;

    /// <summary>
    /// Gets the logical primary credential reference.
    /// </summary>
    public string? CredentialReference { get; init; }

    /// <summary>
    /// Gets the optional logical symbol credential reference.
    /// </summary>
    public string? SymbolCredentialReference { get; init; }

    /// <summary>
    /// Gets an optional repository-relative NuGet configuration path.
    /// </summary>
    public string? ConfigFilePath { get; init; }

    /// <summary>
    /// Gets whether NuGet duplicate skipping is explicitly accepted.
    /// </summary>
    public bool SkipDuplicate { get; init; }

    /// <summary>
    /// Gets whether the trusted package checkpoint is enabled.
    /// </summary>
    public bool UsePackagePublicationCheckpoint { get; init; }

    /// <summary>
    /// Gets whether explicit HTTP sources are accepted.
    /// </summary>
    public bool AllowInsecureHttp { get; init; }

    /// <summary>
    /// Gets the timeout for each package command.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}