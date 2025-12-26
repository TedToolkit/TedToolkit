// -----------------------------------------------------------------------
// <copyright file="CleanOutputModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// 清理输出文件夹，这很重要。.
/// </summary>
public sealed class CleanOutputModule : CleanModule<bool>
{
    /// <inheritdoc />
    protected override Task<bool> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var folder = context.GetOutputFolder();
        if (folder.Exists)
            folder.Delete();

        folder.Create();

        return Task.FromResult(true);
    }
}