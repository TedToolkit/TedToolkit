// -----------------------------------------------------------------------
// <copyright file="EnvironmentPipelineSecretResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Resolves validated environment-variable references on demand.
/// </summary>
internal sealed partial class EnvironmentPipelineSecretResolver : IPipelineSecretResolver
{
    [GeneratedRegex(
        "^[A-Za-z_][A-Za-z0-9_]{0,127}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    /// <inheritdoc/>
    public ValueTask<string?> ResolveAsync(
        string credentialReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = credentialReference.Trim();

        if (!ReferencePattern().IsMatch(reference))
        {
            throw new InvalidDataException(
                "The environment credential reference is invalid.");
        }

        return ValueTask.FromResult(
            Environment.GetEnvironmentVariable(reference));
    }
}