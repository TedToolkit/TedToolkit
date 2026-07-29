// -----------------------------------------------------------------------
// <copyright file="PipelineActionOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Contains the package-owned action switches.
/// </summary>
public sealed record PipelineActionOptions
{
    /// <summary>
    /// Gets whether NuGet packages may be pushed.
    /// </summary>
    public bool PushNuGetPackages { get; init; }

    /// <summary>
    /// Gets whether publish archives may be uploaded.
    /// </summary>
    public bool PublishArtifacts { get; init; }

    /// <summary>
    /// Gets whether a change request may be created or updated.
    /// </summary>
    public bool CreateChangeRequest { get; init; }

    /// <summary>
    /// Gets whether a repository Release may be created or reused.
    /// </summary>
    public bool CreateRelease { get; init; }

    /// <summary>
    /// Gets whether neutral events may be sent to registered sinks.
    /// </summary>
    public bool EmitNotifications { get; init; }

    /// <summary>
    /// Gets the timeout applied to each sink and event.
    /// </summary>
    public TimeSpan NotificationTimeout { get; init; } =
        TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets how nonterminal sink failures affect execution.
    /// </summary>
    public NotificationFailureMode NotificationFailureMode { get; init; } =
        NotificationFailureMode.Continue;
}