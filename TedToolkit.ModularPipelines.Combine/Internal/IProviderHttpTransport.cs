// -----------------------------------------------------------------------
// <copyright file="IProviderHttpTransport.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Defines the internal redirect-free provider HTTP boundary.
/// </summary>
internal interface IProviderHttpTransport
{
    /// <summary>
    /// Sends one bounded request without following redirects.
    /// </summary>
    /// <param name="request">The fully constructed request.</param>
    /// <param name="timeout">The request timeout.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The response whose ownership transfers to the caller.</returns>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}