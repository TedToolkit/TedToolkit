// -----------------------------------------------------------------------
// <copyright file="FileStreamHttpContent.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Streams one immutable file into an HTTP request without buffering it.
/// </summary>
/// <param name="file">The existing file to stream.</param>
internal sealed class FileStreamHttpContent(FileInfo file) : HttpContent
{
    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context)
    {
        _ = context;
        var source = file.OpenRead();
        await using (source.ConfigureAwait(false))
        {
            await source.CopyToAsync(stream).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = file.Length;
        return true;
    }
}