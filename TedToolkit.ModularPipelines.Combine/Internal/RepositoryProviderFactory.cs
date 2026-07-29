// -----------------------------------------------------------------------
// <copyright file="RepositoryProviderFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Creates only the selected internal repository provider.
/// </summary>
/// <param name="secretResolver">The replaceable logical secret resolver.</param>
/// <param name="transport">The redirect-free HTTP transport.</param>
internal sealed class RepositoryProviderFactory(
    IPipelineSecretResolver secretResolver,
    IProviderHttpTransport transport) : IRepositoryProviderFactory
{
    /// <inheritdoc/>
    public async ValueTask<IRepositoryProvider> CreateAsync(
        RepositoryProviderKind kind,
        CombineOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (kind == RepositoryProviderKind.GitHub)
        {
            var connection = ProviderConnectionResolver.ResolveGitHub(
                options.GitHub
                ?? throw new InvalidDataException(
                    "Pipeline:GitHub is required."));
            var token = connection.AuthenticationMode
                        == GitHubAuthenticationMode.ActionsToken
                ? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                : await secretResolver.ResolveAsync(
                        connection.CredentialReference!,
                        cancellationToken)
                    .ConfigureAwait(false);
            return new GitHubRepositoryProvider(
                connection,
                ValidateToken(token, "GitHub"),
                transport);
        }

        if (kind == RepositoryProviderKind.GitLab)
        {
            var connection = ProviderConnectionResolver.ResolveGitLab(
                options.GitLab
                ?? throw new InvalidDataException(
                    "Pipeline:GitLab is required."));
            var token = connection.AuthenticationMode
                        == GitLabAuthenticationMode.JobToken
                ? Environment.GetEnvironmentVariable("CI_JOB_TOKEN")
                : await secretResolver.ResolveAsync(
                        connection.CredentialReference!,
                        cancellationToken)
                    .ConfigureAwait(false);
            return new GitLabRepositoryProvider(
                connection,
                ValidateToken(token, "GitLab"),
                transport);
        }

        throw new InvalidDataException(
            "Pipeline:RepositoryProvider is invalid.");
    }

    private static string ValidateToken(string? token, string provider)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && !token.Any(char.IsControl))
        {
            return token;
        }

        throw new InvalidDataException(
            $"{provider} authentication is unavailable.");
    }
}