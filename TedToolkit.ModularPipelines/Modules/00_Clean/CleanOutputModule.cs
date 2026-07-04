// -----------------------------------------------------------------------
// <copyright file="CleanOutputModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Cleans the output folder before the pipeline runs.
/// </summary>
public sealed class CleanOutputModule : CleanModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var folder = context.GetOutputFolder();
        if (folder.Exists)
        {
            await folder.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        await folder.CreateAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}