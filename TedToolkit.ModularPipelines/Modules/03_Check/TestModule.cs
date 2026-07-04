// -----------------------------------------------------------------------
// <copyright file="TestModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;

using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Test pipeline module that runs tests through dotnet run instead of dotnet test.
/// </summary>
/// <param name="dotnet">The .NET options.</param>
/// <param name="files">The pipeline files.</param>
public sealed class TestModule(IOptions<DotNetPipelineOptions> dotnet, PipelineFiles files) : CheckModule<FileInfo[]>
{
    /// <inheritdoc />
    protected override async Task<FileInfo[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        var resultFolder = context.GetTestFolder();

        var trxFiles = await Task.WhenAll(files.TestFiles
            .Select(async p =>
            {
                if (p.Directory is null)
                {
                    return null;
                }

                await context.DotNet().Run(
                    new()
                    {
                        Project = p.FullName,
                        Arguments = ["--report-trx",],
                        Configuration = dotnet.Value.Configuration,
                    },
                    new() { ThrowOnNonZeroExitCode = false, },
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var folder = p.Directory
                    .CreateSubdirectory("bin")
                    .CreateSubdirectory(dotnet.Value.Configuration);

                var file = folder.GetFiles("*.trx", SearchOption.AllDirectories)
                    .MaxBy(file => file.CreationTime);
                if (file is null)
                {
                    return null;
                }

                var projectName = Path.GetFileNameWithoutExtension(p.Name);
                var path = Path.Combine(resultFolder.Path, projectName + "_" + file.Name);
                File.Copy(file.FullName, path);
                return new FileInfo(path);
            }))
            .ConfigureAwait(false);

        return trxFiles.OfType<FileInfo>().ToArray();
    }
}