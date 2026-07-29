// -----------------------------------------------------------------------
// <copyright file="FormatScope.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents which tracked source paths an explicitly enabled format operation selects.
/// </summary>
public enum FormatScope
{
    /// <summary>
    /// Selects only tracked unstaged changes from the index to the working tree.
    /// </summary>
    WorkingTreeTracked = 0,

    /// <summary>
    /// Selects tracked staged and unstaged changes relative to <c>HEAD</c>.
    /// </summary>
    HeadTracked = 1,

    /// <summary>
    /// Selects the complete declared solution or project.
    /// </summary>
    All = 2,
}