// -----------------------------------------------------------------------
// <copyright file="ProviderConnectionResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Resolves explicit provider settings with provider-owned CI fallbacks.
/// </summary>
internal static class ProviderConnectionResolver
{
    /// <summary>
    /// Resolves and validates GitHub settings.
    /// </summary>
    /// <param name="options">The explicit settings.</param>
    /// <returns>The non-secret resolved connection.</returns>
    /// <exception cref="InvalidDataException">
    /// Required GitHub settings are missing or unsafe.
    /// </exception>
    public static ResolvedGitHubConnection ResolveGitHub(
        GitHubConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var inActions = IsTrue(Environment.GetEnvironmentVariable(
            "GITHUB_ACTIONS"));
        var repositoryParts = inActions
            ? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
                ?.Split('/', StringSplitOptions.RemoveEmptyEntries)
            : null;
        var owner = options.Owner
                    ?? (repositoryParts?.Length == 2
                        ? repositoryParts[0]
                        : null);
        var repository = options.Repository
                         ?? (repositoryParts?.Length == 2
                             ? repositoryParts[1]
                             : null);
        var instance = options.InstanceUrl
                       ?? (inActions
                           ? TryAbsolute(Environment.GetEnvironmentVariable(
                               "GITHUB_SERVER_URL"))
                           : null);
        var api = options.ApiUrl
                  ?? (inActions
                      ? TryAbsolute(Environment.GetEnvironmentVariable(
                          "GITHUB_API_URL"))
                      : null);

        ValidateBase(
            instance,
            options.AllowInsecureHttp,
            "GitHub:InstanceUrl");
        ValidateBase(api, options.AllowInsecureHttp, "GitHub:ApiUrl");
        ValidateSegment(owner, "GitHub:Owner");
        ValidateSegment(repository, "GitHub:Repository");
        ValidateTimeout(options.RequestTimeout, "GitHub:RequestTimeout");

        if (!Enum.IsDefined(options.AuthenticationMode))
        {
            throw Invalid("GitHub:AuthenticationMode");
        }

        if (options.AuthenticationMode == GitHubAuthenticationMode.Token)
        {
            ValidateReference(
                options.CredentialReference,
                "GitHub:CredentialReference");
        }
        else if (options.CredentialReference is not null)
        {
            throw Invalid("GitHub:CredentialReference");
        }

        return new(
            instance!,
            api!,
            owner!,
            repository!,
            options.AuthenticationMode,
            options.CredentialReference,
            options.RequestTimeout);
    }

    /// <summary>
    /// Resolves and validates GitLab settings.
    /// </summary>
    /// <param name="options">The explicit settings.</param>
    /// <returns>The non-secret resolved connection.</returns>
    /// <exception cref="InvalidDataException">
    /// Required GitLab settings are missing or unsafe.
    /// </exception>
    public static ResolvedGitLabConnection ResolveGitLab(
        GitLabConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var inGitLab = IsTrue(Environment.GetEnvironmentVariable(
            "GITLAB_CI"));
        var instance = options.InstanceUrl
                       ?? (inGitLab
                           ? TryAbsolute(Environment.GetEnvironmentVariable(
                               "CI_SERVER_URL"))
                           : null);
        var api = options.ApiUrl
                  ?? (inGitLab
                      ? TryAbsolute(Environment.GetEnvironmentVariable(
                          "CI_API_V4_URL"))
                      : null);
        var projectId = options.ProjectId
                        ?? (inGitLab
                            && long.TryParse(
                                Environment.GetEnvironmentVariable(
                                    "CI_PROJECT_ID"),
                                System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var parsed)
                            ? parsed
                            : null);

        ValidateBase(
            instance,
            options.AllowInsecureHttp,
            "GitLab:InstanceUrl");
        ValidateBase(api, options.AllowInsecureHttp, "GitLab:ApiUrl");

        if (projectId is null or <= 0)
        {
            throw Invalid("GitLab:ProjectId");
        }

        ValidateTimeout(options.RequestTimeout, "GitLab:RequestTimeout");

        if (!Enum.IsDefined(options.AuthenticationMode))
        {
            throw Invalid("GitLab:AuthenticationMode");
        }

        if (options.AuthenticationMode is GitLabAuthenticationMode.PrivateToken
                or GitLabAuthenticationMode.OAuthToken)
        {
            ValidateReference(
                options.CredentialReference,
                "GitLab:CredentialReference");
        }
        else if (options.CredentialReference is not null)
        {
            throw Invalid("GitLab:CredentialReference");
        }

        return new(
            instance!,
            api!,
            projectId.Value,
            options.AuthenticationMode,
            options.CredentialReference,
            options.RequestTimeout);
    }

    private static void ValidateBase(
        Uri? value,
        bool allowInsecureHttp,
        string path)
    {
        if (value?.IsAbsoluteUri == true
            && string.IsNullOrEmpty(value.UserInfo)
            && string.IsNullOrEmpty(value.Query)
            && string.IsNullOrEmpty(value.Fragment)
            && (value.Scheme == Uri.UriSchemeHttps
                || (allowInsecureHttp
                    && value.Scheme == Uri.UriSchemeHttp)))
        {
            return;
        }

        throw Invalid(path);
    }

    private static void ValidateSegment(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsControl)
            && value is not "." and not "..")
        {
            return;
        }

        throw Invalid(path);
    }

    private static void ValidateReference(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && !value.Any(char.IsControl))
        {
            return;
        }

        throw Invalid(path);
    }

    private static void ValidateTimeout(in TimeSpan value, string path)
    {
        if (value >= TimeSpan.FromSeconds(1)
            && value <= TimeSpan.FromMinutes(10))
        {
            return;
        }

        throw Invalid(path);
    }

    private static Uri? TryAbsolute(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static bool IsTrue(string? value)
    {
        return string.Equals(
            value,
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static InvalidDataException Invalid(string path)
    {
        return new($"Pipeline:{path} is invalid or missing.");
    }
}