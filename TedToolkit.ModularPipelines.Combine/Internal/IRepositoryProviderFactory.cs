// -----------------------------------------------------------------------
// <copyright file="IRepositoryProviderFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Creates only the explicitly selected internal provider.
/// </summary>
internal interface IRepositoryProviderFactory
{
    /// <summary>
    /// Creates the selected provider without exposing its SDK.
    /// </summary>
    /// <param name="kind">The selected provider.</param>
    /// <param name="options">Validated options.</param>
    /// <param name="cancellationToken">A token that cancels creation.</param>
    /// <returns>The internal provider.</returns>
    ValueTask<IRepositoryProvider> CreateAsync(
        RepositoryProviderKind kind,
        CombineOptions options,
        CancellationToken cancellationToken);
}