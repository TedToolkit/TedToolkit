// -----------------------------------------------------------------------
// <copyright file="IPipelineSecretResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Resolves a logical credential only for its enabled owning action.
/// </summary>
public interface IPipelineSecretResolver
{
    /// <summary>
    /// Resolves one logical reference.
    /// </summary>
    /// <param name="credentialReference">The non-secret logical reference.</param>
    /// <param name="cancellationToken">A token that cancels resolution.</param>
    /// <returns>The secret value, or <see langword="null"/> when absent.</returns>
    ValueTask<string?> ResolveAsync(
        string credentialReference,
        CancellationToken cancellationToken);
}