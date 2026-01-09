// -----------------------------------------------------------------------
// <copyright file="DotnetBuildModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.IO.Compression;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.Logging;
using ModularPipelines.Modules;

using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Compile.
/// </summary>
/// <param name="options">dotnet options.</param>
/// <param name="files">files.</param>
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(CompileModule<>))]
public sealed partial class DotnetBuildModule(IOptions<DotNetPipelineOptions> options, PipelineFiles files)
    : Module<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        var nugetFolder = context.GetNugetFolder();
        var failedNames = await Task.WhenAll(files.BuildFiles
                .Select(async p =>
                {
                    await context.DotNet().Clean(
                            new(p.FullName) { Configuration = options.Value.Configuration, },
                            cancellationToken)
                        .ConfigureAwait(false);

                    var result = await context.DotNet()
                        .Build(
                            new()
                            {
                                ProjectSolution = p.FullName,
                                Configuration = options.Value.Configuration,
                                Arguments = [$"/p:PackageOutputPath={nugetFolder.Path}",],
                                ThrowOnNonZeroExitCode = false,
                            },
                            token: cancellationToken)
                        .ConfigureAwait(false);

                    if (result.ExitCode is 0)
                        return "";

                    LogFailed(context.Logger, p.FullName);

                    var fileToSave = context.GetOutputFolder().GetFile(p.Name + ".txt");
                    fileToSave.Delete();
                    await fileToSave.WriteAsync(result.StandardOutput, cancellationToken).ConfigureAwait(false);

                    return Path.GetFileNameWithoutExtension(p.Name);
                }))
            .ConfigureAwait(false);

        await context.GetFailedFile().WriteAsync(
                string.Join(Environment.NewLine, failedNames.Where(i => !string.IsNullOrEmpty(i))), cancellationToken)
            .ConfigureAwait(false);

        foreach (var file in nugetFolder.GetFiles("*.nupkg"))
        {
            var name = file.NameWithoutExtension;
#pragma warning disable CA2007
            await using (var stream = file.GetStream(FileAccess.Read))
#pragma warning restore CA2007
            {
                ZipFile.ExtractToDirectory(
                    stream,
                    nugetFolder.CreateFolder(name).Path,
                    true);
            }

            file.Delete();
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to build {ProjectPath}")]
    private static partial void LogFailed(IModuleLogger logger, string projectPath);
}