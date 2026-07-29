// -----------------------------------------------------------------------
// <copyright file="ProviderHttpTransport.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Sends provider requests through one redirect-free client.
/// </summary>
internal sealed class ProviderHttpTransport : IProviderHttpTransport, IDisposable
{
    private readonly HttpClientHandler _handler;

    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderHttpTransport"/> class.
    /// </summary>
    public ProviderHttpTransport()
    {
        _handler = new();
        _handler.AllowAutoRedirect = false;
        _handler.CheckCertificateRevocationList = true;
        _client = new(_handler, disposeHandler: false);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .WaitAsync(timeout, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}