// -----------------------------------------------------------------------
// <copyright file="ChangeDescriptionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Descriptions;

/// <summary>
/// Represents the independently composed kinds of neutral change descriptions.
/// </summary>
public enum ChangeDescriptionKind
{
    /// <summary>
    /// Indicates a commit subject and optional commit body.
    /// </summary>
    CommitMessage = 0,

    /// <summary>
    /// Indicates a change-request title and body.
    /// </summary>
    ChangeRequest = 1,
}