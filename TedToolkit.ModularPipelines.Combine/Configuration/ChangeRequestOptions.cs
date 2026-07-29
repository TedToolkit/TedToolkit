// -----------------------------------------------------------------------
// <copyright file="ChangeRequestOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains deterministic change-request policy.
/// </summary>
public sealed record ChangeRequestOptions
{
    /// <summary>
    /// Gets the development-to-main fallback title.
    /// </summary>
    public string ReleaseTitle { get; init; } = "Release";

    /// <summary>
    /// Gets whether a newly created change request is a draft.
    /// </summary>
    public bool Draft { get; init; } = true;

    /// <summary>
    /// Gets the requested labels.
    /// </summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>
    /// Gets whether recognized issue-closing lines are preserved.
    /// </summary>
    public bool PreserveIssueClosingDirectives { get; init; } = true;
}