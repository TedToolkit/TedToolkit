// -----------------------------------------------------------------------
// <copyright file="GitLabAuthenticationMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects GitLab credential lookup and header behavior.
/// </summary>
public enum GitLabAuthenticationMode
{
    /// <summary>
    /// Reads the built-in GitLab job token.
    /// </summary>
    JobToken = 0,

    /// <summary>
    /// Resolves a private-token reference.
    /// </summary>
    PrivateToken = 1,

    /// <summary>
    /// Resolves an OAuth bearer-token reference.
    /// </summary>
    OAuthToken = 2,
}