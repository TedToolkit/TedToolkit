// -----------------------------------------------------------------------
// <copyright file="PublishModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Attributes;

using TedToolkit.ModularPipelines.Attributes;
using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Publish the files.
/// </summary>
/// <param name="files">files.</param>
/// <param name="dotnet">dotnet.</param>
[RunOnGithubActionOnly]
[RunIfBranch(SharedHelpers.MAIN_BRANCH)]
public sealed class PublishModule(PipelineFiles files, IOptions<DotNetPipelineOptions> dotnet) : CheckModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var publishFolder = context.GetPublishFolder();
        await Task.WhenAll(files.PublishFiles.Select(f => context.SubModule(f.File.Name,
            async () =>
            {
                var publishName = Path.GetFileNameWithoutExtension(f.File.Name);
                var relayFolder = publishFolder.CreateFolder(publishName);

                await context.DotNet()
                    .Publish(
                        new DotNetPublishOptions()
                        {
                            ProjectSolution = f.File.FullName,
                            Configuration = dotnet.Value.Configuration,
                            Framework = f.Framework,
                            Output = relayFolder.Path,
                        }, cancellationToken: cancellationToken).ConfigureAwait(false);

                var zipName = $"{publishName}_{RuntimeInformation.RuntimeIdentifier}";
                if (!string.IsNullOrEmpty(f.Framework))
                {
                    zipName += "_" + f.Framework;
                }

                var zipFile = publishFolder.CreateFile($"{zipName}.zip");
#pragma warning disable CA2007
                await using (var stream = zipFile.GetStream())
#pragma warning restore CA2007
                    await ZipFile.CreateFromDirectoryAsync(relayFolder.Path, stream, cancellationToken).ConfigureAwait(false);

                await relayFolder.DeleteAsync(cancellationToken).ConfigureAwait(false);
            }))).ConfigureAwait(false);

        return true;
    }
}