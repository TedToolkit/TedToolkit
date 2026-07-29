// -----------------------------------------------------------------------
// <copyright file="ResolvedGitLabConnection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Contains one validated GitLab connection without a credential value.
/// </summary>
/// <param name="InstanceUrl">The web base.</param>
/// <param name="ApiUrl">The API base.</param>
/// <param name="ProjectId">The numeric project identifier.</param>
/// <param name="AuthenticationMode">The credential mode.</param>
/// <param name="CredentialReference">The optional logical reference.</param>
/// <param name="RequestTimeout">The request timeout.</param>
internal sealed record ResolvedGitLabConnection(
    Uri InstanceUrl,
    Uri ApiUrl,
    long ProjectId,
    GitLabAuthenticationMode AuthenticationMode,
    string? CredentialReference,
    TimeSpan RequestTimeout);