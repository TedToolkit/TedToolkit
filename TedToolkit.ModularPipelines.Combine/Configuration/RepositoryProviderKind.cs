// -----------------------------------------------------------------------
// <copyright file="RepositoryProviderKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Identifies the selected repository provider.
/// </summary>
public enum RepositoryProviderKind
{
    /// <summary>
    /// Uses GitHub.
    /// </summary>
    GitHub = 0,

    /// <summary>
    /// Uses GitLab.
    /// </summary>
    GitLab = 1,
}