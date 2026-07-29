using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace TedToolkit.CodeAnalysis.PackageTests;

internal sealed class CodeFixLoadingTests
{
    /// <summary>
    /// 验证每个保留的 Roslyn 主机版本都能从离线闭包发现并实例化全部 CodeFix 提供程序。
    /// </summary>
    [Test]
    [NotInParallel("CodeAnalysisPack")]
    public async Task Should_load_every_preserved_code_fix_provider_in_each_exact_host()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.CodeAnalysis.CodeFixTests",
            Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(fixtureRoot, "package");
        var upstreamSource = Path.Combine(fixtureRoot, "upstream-source");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(upstreamSource);

        try
        {
            await PackAsync(repositoryRoot, fixtureRoot);
            ZipFile.ExtractToDirectory(
                Path.Combine(
                    fixtureRoot,
                    "TedToolkit.CodeAnalysis.2026.7.29.nupkg"),
                packageRoot);

            var dependencyRoots = Path.Combine(
                repositoryRoot,
                "tests",
                "CodeFixHosts");
            CopyPackageClosure(
                Path.Combine(
                    dependencyRoots,
                    "Roslyn38Dependencies",
                    "obj",
                    "project.assets.json"),
                upstreamSource);
            CopyPackageClosure(
                Path.Combine(
                    dependencyRoots,
                    "Roslyn47Dependencies",
                    "obj",
                    "project.assets.json"),
                upstreamSource);

            var nugetConfigPath = Path.Combine(fixtureRoot, "NuGet.Config");
            new XDocument(
                new XElement(
                    "configuration",
                    new XElement(
                        "packageSources",
                        new XElement("clear"),
                        new XElement(
                            "add",
                            new XAttribute("key", "upstream"),
                            new XAttribute("value", upstreamSource)))))
                .Save(nugetConfigPath);

            var neutralRoot = Path.Combine(
                packageRoot,
                "analyzers",
                "dotnet",
                "cs");
            await AssertExactHostAsync(
                repositoryRoot,
                fixtureRoot,
                nugetConfigPath,
                "3.8.0",
                Path.Combine(
                    packageRoot,
                    "analyzers",
                    "dotnet",
                    "roslyn3.8",
                    "cs"),
                neutralRoot);
            await AssertExactHostAsync(
                repositoryRoot,
                fixtureRoot,
                nugetConfigPath,
                "4.7.0",
                Path.Combine(
                    packageRoot,
                    "analyzers",
                    "dotnet",
                    "roslyn4.7",
                    "cs"),
                neutralRoot);
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    private static async Task AssertExactHostAsync(
        string repositoryRoot,
        string fixtureRoot,
        string nugetConfigPath,
        string roslynVersion,
        string variantRoot,
        string neutralRoot)
    {
        var hostRoot = Path.Combine(
            fixtureRoot,
            $"host-{roslynVersion}");
        Directory.CreateDirectory(hostRoot);
        File.Copy(
            Path.Combine(
                repositoryRoot,
                "tests",
                "CodeFixHosts",
                "Program.cs"),
            Path.Combine(hostRoot, "Program.cs"));
        await File.WriteAllTextAsync(
            Path.Combine(hostRoot, "Host.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <OutputType>Exe</OutputType>
                 <TargetFramework>net10.0</TargetFramework>
                 <ImplicitUsings>enable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces"
                                   Version="{roslynVersion}" />
               </ItemGroup>
             </Project>
             """);

        var restoreResult = await RunDotNetAsync(
            hostRoot,
            "restore",
            "Host.csproj",
            "--configfile",
            nugetConfigPath,
            "--packages",
            Path.Combine(hostRoot, "packages"),
            "--no-http-cache");
        EnsureSucceeded(restoreResult);
        await Assert.That(restoreResult.Output).DoesNotContain("http://");
        await Assert.That(restoreResult.Output).DoesNotContain("https://");

        var runResult = await RunDotNetAsync(
            hostRoot,
            "run",
            "--project",
            "Host.csproj",
            "--configuration",
            "Release",
            "--no-restore",
            "--",
            variantRoot,
            neutralRoot,
            "5");
        EnsureSucceeded(runResult);

        var json = runResult.Output
            .Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries)
            .Last(line => line.StartsWith('{'));
        using var document = JsonDocument.Parse(json);

        await Assert.That(document.RootElement
                .GetProperty("assemblies")
                .GetInt32())
            .IsEqualTo(5);
        await Assert.That(document.RootElement
                .GetProperty("providers")
                .GetInt32())
            .IsGreaterThan(0);
    }

    private static void CopyPackageClosure(
        string assetsPath,
        string destination)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        var packageFolders = document.RootElement
            .GetProperty("packageFolders")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        foreach (var library in document.RootElement
                     .GetProperty("libraries")
                     .EnumerateObject())
        {
            if (!string.Equals(
                    library.Value.GetProperty("type").GetString(),
                    "package",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var separator = library.Name.LastIndexOf('/');
            var packageId = library.Name[..separator].ToLowerInvariant();
            var version = library.Name[(separator + 1)..].ToLowerInvariant();
            var relativePackagePath = Path.Combine(
                packageId,
                version,
                $"{packageId}.{version}.nupkg");
            var packagePath = packageFolders
                .Select(root => Path.Combine(root, relativePackagePath))
                .Single(File.Exists);
            var destinationPath = Path.Combine(
                destination,
                Path.GetFileName(packagePath));

            if (!File.Exists(destinationPath))
            {
                File.Copy(packagePath, destinationPath);
            }
        }
    }

    private static async Task PackAsync(
        string repositoryRoot,
        string outputRoot)
    {
        var result = await RunDotNetAsync(
            repositoryRoot,
            "pack",
            Path.Combine(
                repositoryRoot,
                "TedToolkit.CodeAnalysis",
                "TedToolkit.CodeAnalysis.csproj"),
            "--configuration",
            "Release",
            "--output",
            outputRoot,
            "-p:PackageVersion=2026.7.29");
        EnsureSucceeded(result);
    }

    private static async Task<ProcessResult> RunDotNetAsync(
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

        return new ProcessResult(
            process.ExitCode,
            $"{await standardOutput}{await standardError}");
    }

    private static void EnsureSucceeded(ProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Output);
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

    private sealed record ProcessResult(int ExitCode, string Output);
}
