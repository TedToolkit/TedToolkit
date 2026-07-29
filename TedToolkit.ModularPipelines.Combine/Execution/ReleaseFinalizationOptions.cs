// -----------------------------------------------------------------------
// <copyright file="ReleaseFinalizationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Execution;

/// <summary>
/// Contains an explicitly authorized Release-only recovery operation.
/// </summary>
public sealed record ReleaseFinalizationOptions
{
    /// <summary>
    /// Gets the selected provider.
    /// </summary>
    public required RepositoryProviderKind Provider { get; init; }

    /// <summary>
    /// Gets the empty-artifact neutral Release request.
    /// </summary>
    public required ReleasePublicationRequest Request { get; init; }

    /// <summary>
    /// Gets GitHub configuration when GitHub is selected.
    /// </summary>
    public GitHubConnectionOptions? GitHub { get; init; }

    /// <summary>
    /// Gets GitLab configuration when GitLab is selected.
    /// </summary>
    public GitLabConnectionOptions? GitLab { get; init; }
}