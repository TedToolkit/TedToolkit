// -----------------------------------------------------------------------
// <copyright file="ResolvedGitHubConnection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Contains one validated GitHub connection without a credential value.
/// </summary>
/// <param name="InstanceUrl">The web base.</param>
/// <param name="ApiUrl">The API base.</param>
/// <param name="Owner">The repository owner.</param>
/// <param name="Repository">The repository name.</param>
/// <param name="AuthenticationMode">The credential mode.</param>
/// <param name="CredentialReference">The optional logical reference.</param>
/// <param name="RequestTimeout">The request timeout.</param>
internal sealed record ResolvedGitHubConnection(
    Uri InstanceUrl,
    Uri ApiUrl,
    string Owner,
    string Repository,
    GitHubAuthenticationMode AuthenticationMode,
    string? CredentialReference,
    TimeSpan RequestTimeout);