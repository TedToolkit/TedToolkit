// -----------------------------------------------------------------------
// <copyright file="ReleaseFinalizer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Execution;

/// <summary>
/// Implements the isolated Release-only recovery boundary.
/// </summary>
/// <param name="providerFactory">The reviewed internal provider factory.</param>
internal sealed class ReleaseFinalizer(
    IRepositoryProviderFactory providerFactory) : IReleaseFinalizer
{
    /// <inheritdoc/>
    public async Task<ReleasePublicationResult> FinalizeAsync(
        ReleaseFinalizationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Request);

        if (options.Request.Artifacts.Count != 0
            || (options.Provider == RepositoryProviderKind.GitHub
            && (options.GitHub is null || options.GitLab is not null))
            || (options.Provider == RepositoryProviderKind.GitLab
            && (options.GitLab is null || options.GitHub is not null))
            || !Enum.IsDefined(options.Provider))
        {
            throw new InvalidDataException(
                "Release finalization configuration is invalid.");
        }

        var version = NuGetVersion.Parse(options.Request.Version);

        if (!version.ToNormalizedString().Equals(
                options.Request.Version,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(options.Request.TagName)
            || string.IsNullOrWhiteSpace(options.Request.TargetRevision)
            || string.IsNullOrWhiteSpace(options.Request.Title))
        {
            throw new InvalidDataException(
                "Release finalization request is invalid.");
        }

        var combineOptions = new CombineOptions()
        {
            RepositoryProvider = options.Provider,
            GitHub = options.GitHub,
            GitLab = options.GitLab,
        };
        var provider = await providerFactory.CreateAsync(
                options.Provider,
                combineOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return await provider.FinalizeReleaseAsync(
                options.Request,
                cancellationToken)
            .ConfigureAwait(false);
    }
}