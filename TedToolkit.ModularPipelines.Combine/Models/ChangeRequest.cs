// -----------------------------------------------------------------------
// <copyright file="ChangeRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Models;

/// <summary>
/// Describes a change request returned by a provider.
/// </summary>
public sealed record ChangeRequest
{
    /// <summary>
    /// Gets the provider identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the source branch.
    /// </summary>
    public required string SourceBranch { get; init; }

    /// <summary>
    /// Gets the target branch.
    /// </summary>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// Gets the final title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the canonical request URI.
    /// </summary>
    public required Uri Uri { get; init; }

    /// <summary>
    /// Gets the request state.
    /// </summary>
    public required ChangeRequestState State { get; init; }

    /// <summary>
    /// Gets whether the request is a draft.
    /// </summary>
    public bool IsDraft { get; init; }

    /// <summary>
    /// Gets the final labels.
    /// </summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>
    /// Gets the idempotent operation disposition.
    /// </summary>
    public required OperationDisposition Disposition { get; init; }
}