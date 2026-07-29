// -----------------------------------------------------------------------
// <copyright file="CreateChangeRequestRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Represents a provider-neutral change-request mutation.
/// </summary>
public sealed record CreateChangeRequestRequest
{
    /// <summary>
    /// Gets the source branch.
    /// </summary>
    public required string SourceBranch { get; init; }

    /// <summary>
    /// Gets the target branch.
    /// </summary>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the body.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets whether a new request is a draft.
    /// </summary>
    public bool Draft { get; init; }

    /// <summary>
    /// Gets the normalized labels.
    /// </summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>
    /// Gets whether recognized issue-closing lines from an existing request
    /// must be retained exactly once.
    /// </summary>
    public bool PreserveIssueClosingDirectives { get; init; } = true;
}