using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace TedToolkit.CodeAnalysis.PackageTests;

internal sealed class PackageContentTests
{
    /// <summary>
    /// 验证生成包包含完整审计资产、精确 Sonar 依赖和唯一允许的 MSBuild 激活文件。
    /// </summary>
    [Test]
    [NotInParallel("CodeAnalysisPack")]
    public async Task Should_pack_only_the_approved_code_analysis_contract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectRoot = Path.Combine(repositoryRoot, "TedToolkit.CodeAnalysis");
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.CodeAnalysis.PackageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);

        try
        {
            await RunDotNetAsync(
                repositoryRoot,
                "pack",
                Path.Combine(projectRoot, "TedToolkit.CodeAnalysis.csproj"),
                "--configuration",
                "Release",
                "--output",
                outputRoot,
                "-p:PackageVersion=2026.7.29");

            var packagePath = Path.Combine(
                outputRoot,
                "TedToolkit.CodeAnalysis.2026.7.29.nupkg");

            await Assert.That(File.Exists(packagePath)).IsTrue();

            using var archive = ZipFile.OpenRead(packagePath);
            var paths = archive.Entries
                .Select(entry => entry.FullName)
                .ToHashSet(StringComparer.Ordinal);
            var inventory = (await File.ReadAllLinesAsync(
                    Path.Combine(projectRoot, "analyzer-assets.csv")))
                .Skip(1)
                .Select(line =>
                {
                    var fields = line.Split(',');
                    return new
                    {
                        PackagePath = fields[4],
                        Sha256 = fields[5],
                    };
                })
                .ToArray();

            foreach (var expectedAsset in inventory)
            {
                await Assert.That(paths).Contains(expectedAsset.PackagePath);
                var entry = archive.GetEntry(expectedAsset.PackagePath)!;
                await using var entryStream = entry.Open();
                var hash = Convert.ToHexString(
                        await SHA256.HashDataAsync(entryStream))
                    .ToLowerInvariant();

                await Assert.That(hash).IsEqualTo(expectedAsset.Sha256);
            }

            await Assert.That(paths.Where(path =>
                    path.StartsWith(
                        "analyzers/dotnet/",
                        StringComparison.OrdinalIgnoreCase)
                    && path.EndsWith(
                        ".dll",
                        StringComparison.OrdinalIgnoreCase)))
                .Count()
                .IsEqualTo(inventory.Length);

            await Assert.That(paths).Contains("README.md");
            await Assert.That(paths).Contains("Icon.jpg");
            await Assert.That(paths).Contains("LICENSE.txt");
            await Assert.That(paths).Contains("THIRD-PARTY-NOTICES.txt");
            await Assert.That(paths).Contains("analyzer-assets.csv");
            await Assert.That(paths).Contains("analyzer-dependencies.csv");
            await Assert.That(paths).Contains(
                "buildTransitive/TedToolkit.CodeAnalysis.props");
            await Assert.That(paths).Contains(
                "buildTransitive/TedToolkit.CodeAnalysis.targets");

            await Assert.That(paths.Any(path =>
                    path.EndsWith(
                        "SonarAnalyzer.CSharp.dll",
                        StringComparison.OrdinalIgnoreCase)))
                .IsFalse();
            await Assert.That(paths.Any(path =>
                    path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("build/", StringComparison.OrdinalIgnoreCase)))
                .IsFalse();
            await Assert.That(paths.Where(path =>
                    path.StartsWith(
                        "buildTransitive/",
                        StringComparison.OrdinalIgnoreCase)))
                .Count()
                .IsEqualTo(2);
            await Assert.That(paths.Any(path =>
                    path.EndsWith(".editorconfig", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("stylecop.json", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".ruleset", StringComparison.OrdinalIgnoreCase)))
                .IsFalse();

            var nuspecEntry = archive.Entries.Single(entry =>
                entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            await using var nuspecStream = nuspecEntry.Open();
            var nuspec = await XDocument.LoadAsync(
                nuspecStream,
                LoadOptions.None,
                CancellationToken.None);
            var metadata = nuspec.Root!.Elements().Single(element =>
                element.Name.LocalName == "metadata");
            var dependencies = metadata
                .Descendants()
                .Where(element => element.Name.LocalName == "dependency")
                .ToArray();
            await Assert.That(dependencies).Count().IsEqualTo(1);

            var sonarDependency = dependencies.Single();

            await Assert.That((string?)sonarDependency.Attribute("id"))
                .IsEqualTo("SonarAnalyzer.CSharp");
            await Assert.That((string?)sonarDependency.Attribute("version"))
                .IsEqualTo("[10.23.0.137933]");
            var license = metadata.Elements().Single(element =>
                element.Name.LocalName == "license");
            await Assert.That((string?)license.Attribute("type"))
                .IsEqualTo("file");
            await Assert.That(license.Value)
                .IsEqualTo("LICENSE.txt");
            await Assert.That(metadata.Elements().Single(element =>
                    element.Name.LocalName == "readme").Value)
                .IsEqualTo("README.md");
            await Assert.That(metadata.Elements().Single(element =>
                    element.Name.LocalName == "icon").Value)
                .IsEqualTo("Icon.jpg");
            await Assert.That(metadata.Elements().Single(element =>
                    element.Name.LocalName == "projectUrl").Value)
                .IsEqualTo("https://github.com/TedToolkit/TedToolkit");
            var repository = metadata.Elements().Single(element =>
                element.Name.LocalName == "repository");
            await Assert.That((string?)repository.Attribute("type"))
                .IsEqualTo("git");
            await Assert.That((string?)repository.Attribute("url"))
                .IsEqualTo("https://github.com/TedToolkit/TedToolkit");
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static async Task RunDotNetAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException(
                                "The dotnet process could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code "
                + $"{process.ExitCode}.{Environment.NewLine}{output}{error}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "The repository root could not be located.");
    }
}
