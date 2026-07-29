// -----------------------------------------------------------------------
// <copyright file="IChangeDescriptionGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Descriptions;

/// <summary>
/// Represents an optional consumer-owned change-description capability.
/// </summary>
public interface IChangeDescriptionGenerator
{
    /// <summary>
    /// Generates a description from explicitly authorized bounded local change data.
    /// </summary>
    /// <param name="request">The neutral description request.</param>
    /// <param name="cancellationToken">A token that cancels generation.</param>
    /// <returns>A generated description, or <see langword="null"/> when the implementation declines.</returns>
    Task<ChangeDescription?> GenerateAsync(
        ChangeDescriptionRequest request,
        CancellationToken cancellationToken = default);
}