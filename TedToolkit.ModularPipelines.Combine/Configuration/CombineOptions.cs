// -----------------------------------------------------------------------
// <copyright file="CombineOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains the complete provider-neutral Combine configuration.
/// </summary>
public sealed record CombineOptions
{
    /// <summary>
    /// Gets the profile selection policy.
    /// </summary>
    public PipelineExecutionPolicy ExecutionPolicy { get; init; } =
        PipelineExecutionPolicy.Standard;

    /// <summary>
    /// Gets the explicit profile for manual policy.
    /// </summary>
    public PipelineProfile? ManualProfile { get; init; }

    /// <summary>
    /// Gets an explicit neutral execution context.
    /// </summary>
    public PipelineExecutionContext? ExecutionContext { get; init; }

    /// <summary>
    /// Gets the Build input mode.
    /// </summary>
    public PipelineInputMode InputMode { get; init; } =
        PipelineInputMode.RunBuild;

    /// <summary>
    /// Gets the manifest path relative to the explicit consumer root.
    /// </summary>
    public string? ArtifactManifestPath { get; init; }

    /// <summary>
    /// Gets manifest-validation options.
    /// </summary>
    public ArtifactValidationOptions ArtifactValidation { get; init; } = new();

    /// <summary>
    /// Gets explicit action switches.
    /// </summary>
    public PipelineActionOptions Actions { get; init; } = new();

    /// <summary>
    /// Gets NuGet push configuration when that action is enabled.
    /// </summary>
    public NuGetPushOptions? NuGetPush { get; init; }

    /// <summary>
    /// Gets the optional trusted package-publication checkpoint.
    /// </summary>
    /// <remarks>
    /// This runtime service is supplied only through the typed options object.
    /// Package configuration cannot construct or bind it.
    /// </remarks>
    public IPackagePublicationCheckpoint? PackagePublicationCheckpoint { get; init; }

    /// <summary>
    /// Gets caller-supplied, already-bounded neutral commit summaries.
    /// </summary>
    /// <remarks>
    /// This runtime data is supplied through typed options and is never fetched
    /// from a provider by Combine.
    /// </remarks>
    public IReadOnlyList<CommitSummary> Commits { get; init; } = [];

    /// <summary>
    /// Gets publication metadata when a provider action is enabled.
    /// </summary>
    public PublicationOptions? Publication { get; init; }

    /// <summary>
    /// Gets change-request policy.
    /// </summary>
    public ChangeRequestOptions ChangeRequest { get; init; } = new();

    /// <summary>
    /// Gets the selected repository provider.
    /// </summary>
    public RepositoryProviderKind? RepositoryProvider { get; init; }

    /// <summary>
    /// Gets GitHub connection settings.
    /// </summary>
    public GitHubConnectionOptions? GitHub { get; init; }

    /// <summary>
    /// Gets GitLab connection settings.
    /// </summary>
    public GitLabConnectionOptions? GitLab { get; init; }
}