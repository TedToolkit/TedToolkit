// -----------------------------------------------------------------------
// <copyright file="NuGetAuthenticationMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects NuGet authentication behavior.
/// </summary>
public enum NuGetAuthenticationMode
{
    /// <summary>
    /// Resolves and supplies an API key.
    /// </summary>
    ApiKey = 0,

    /// <summary>
    /// Uses the noninteractive NuGet configuration chain.
    /// </summary>
    NuGetConfig = 1,
}