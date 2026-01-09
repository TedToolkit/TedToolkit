// -----------------------------------------------------------------------
// <copyright file="BumpVersionModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Xml.Linq;

using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.GitHub;

using TedToolkit.ModularPipelines.Attributes;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Get the version based on the prop.
/// </summary>
/// <param name="gitHubEnvironment">github environment.</param>
/// <param name="github">github client.</param>
[RunOnGithubActionOnly]
public sealed class BumpVersionModule(IGitHubEnvironmentVariables gitHubEnvironment, IGitHub github)
    : PrepareModule<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var propsFile = context.Git().RootDirectory.GetFile("Directory.Build.props");
        if (propsFile is null)
            throw new FileNotFoundException("Directory.Build.props file not found");

        var version = await ChangeVersionTagAsync(propsFile, v =>
        {
            var today = DateTime.Today;
            if (v.Major == today.Year && v.Minor == today.Month && v.Build == today.Day)
                return new(today.Year, today.Month, today.Day, Math.Max(0, v.Revision) + 1);

            return new(today.Year, today.Month, today.Day, 0);
        }).ConfigureAwait(false);

        await context.GetVersionFile()
            .WriteAsync(version?.ToString() ?? throw new InvalidOperationException(), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private async Task<Version?> ChangeVersionTagAsync(
        global::ModularPipelines.FileSystem.File propsFile,
        Func<Version, Version> changer)
    {
        var doc = XDocument.Load(propsFile.Path);
        var versionElement = doc.Descendants("Version").FirstOrDefault();
        if (versionElement is null)
            return null;

        var releases =
            await github.Client.Repository.Release.GetAll(long.Parse(gitHubEnvironment.RepositoryId!,
                CultureInfo.CurrentCulture)).ConfigureAwait(false);

        if (releases.Count is 0 || !Version.TryParse(releases[0].TagName, out var version))
            version = new(1, 0);

        var newVersion = changer(version);
        versionElement.Value = newVersion.ToString();
        doc.Save(propsFile.Path);
        return newVersion;
    }
}