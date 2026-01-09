// -----------------------------------------------------------------------
// <copyright file="UpdateEditorConfigModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.Models;

using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// update the editor config.
/// </summary>
/// <param name="dotnet">dotnet options.</param>
public sealed class UpdateEditorConfigModule(IOptions<DotNetPipelineOptions> dotnet) : CleanModule<bool>
{
    /// <inheritdoc />
    protected override Task<SkipDecision> ShouldSkip(IPipelineContext context)
    {
        return Task.FromResult(dotnet.Value.SkipUpdateEditorConfig
            ? SkipDecision.Skip("Skip Update EditorConfig.")
            : SkipDecision.DoNotSkip);
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var editorConfigString = await SharedHelpers.GetEditorConfigAsync(cancellationToken).ConfigureAwait(false);
        var file = context.GetRootFolder().GetFile(".editorconfig");
        await file.WriteAsync(editorConfigString, cancellationToken).ConfigureAwait(false);

        return true;
    }
}