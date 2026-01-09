// -----------------------------------------------------------------------
// <copyright file="PublishOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Constants;

/// <summary>
/// options to publish.
/// </summary>
/// <param name="File">file.</param>
/// <param name="Framework">framework.</param>
public readonly record struct PublishOptions(
    FileInfo File,
    string? Framework = null)
{
    /// <summary>
    /// convert form file.
    /// </summary>
    /// <param name="file">file path.</param>
    /// <returns>options.</returns>
#pragma warning disable CA2225
    public static implicit operator PublishOptions(FileInfo file)
#pragma warning restore CA2225
        => new(file);
}