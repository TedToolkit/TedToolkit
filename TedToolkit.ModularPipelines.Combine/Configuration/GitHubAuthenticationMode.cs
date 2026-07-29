// -----------------------------------------------------------------------
// <copyright file="GitHubAuthenticationMode.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Selects GitHub credential lookup behavior.
/// </summary>
public enum GitHubAuthenticationMode
{
    /// <summary>
    /// Reads the built-in Actions token only when required.
    /// </summary>
    ActionsToken = 0,

    /// <summary>
    /// Resolves a logical credential reference.
    /// </summary>
    Token = 1,
}