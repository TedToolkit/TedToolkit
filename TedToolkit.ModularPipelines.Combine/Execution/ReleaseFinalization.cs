// -----------------------------------------------------------------------
// <copyright file="ReleaseFinalization.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Execution;

/// <summary>
/// Provides the explicit provider-neutral Release-only recovery entry point.
/// </summary>
public static class ReleaseFinalization
{
    /// <summary>
    /// Finalizes one Release without running Build, package, asset, change-request,
    /// or notification actions.
    /// </summary>
    /// <param name="options">The explicit Release-only request.</param>
    /// <param name="secretResolver">
    /// An optional resolver for logical personal-token references.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The verified neutral Release result.</returns>
    public static async Task<ReleasePublicationResult> FinalizeAsync(
        ReleaseFinalizationOptions options,
        IPipelineSecretResolver? secretResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        secretResolver ??= new EnvironmentPipelineSecretResolver();
        using var transport = new ProviderHttpTransport();
        var finalizer = new ReleaseFinalizer(
            new RepositoryProviderFactory(secretResolver, transport));
        return await finalizer.FinalizeAsync(options, cancellationToken)
            .ConfigureAwait(false);
    }
}