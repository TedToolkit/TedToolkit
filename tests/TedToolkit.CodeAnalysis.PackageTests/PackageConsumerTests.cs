using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace TedToolkit.CodeAnalysis.PackageTests;

internal sealed class PackageConsumerTests
{
    /// <summary>
    /// 验证离线消费者仅引用候选包时会激活精确 Sonar 资产并服从消费者诊断级别。
    /// </summary>
    [Test]
    [NotInParallel("CodeAnalysisPack")]
    public async Task Should_activate_sonar_and_preserve_consumer_severity_from_local_packages()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectRoot = Path.Combine(repositoryRoot, "TedToolkit.CodeAnalysis");
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.CodeAnalysis.ConsumerTests",
            Guid.NewGuid().ToString("N"));
        var candidateSource = Path.Combine(fixtureRoot, "candidate-source");
        var upstreamSource = Path.Combine(fixtureRoot, "upstream-source");
        var consumerRoot = Path.Combine(fixtureRoot, "consumer");
        var packagesRoot = Path.Combine(fixtureRoot, "packages");
        Directory.CreateDirectory(candidateSource);
        Directory.CreateDirectory(upstreamSource);
        Directory.CreateDirectory(consumerRoot);

        try
        {
            var packResult = await RunDotNetAsync(
                repositoryRoot,
                "pack",
                Path.Combine(projectRoot, "TedToolkit.CodeAnalysis.csproj"),
                "--configuration",
                "Release",
                "--output",
                candidateSource,
                "-p:PackageVersion=2026.7.29");
            await Assert.That(packResult.ExitCode).IsEqualTo(0);

            var packageFolder = ReadPackageFolder(
                Path.Combine(
                    projectRoot,
                    "obj",
                    "project.assets.json"));
            var sonarPackagePath = Path.Combine(
                packageFolder,
                "sonaranalyzer.csharp",
                "10.23.0.137933",
                "sonaranalyzer.csharp.10.23.0.137933.nupkg");
            File.Copy(
                sonarPackagePath,
                Path.Combine(
                    upstreamSource,
                    "sonaranalyzer.csharp.10.23.0.137933.nupkg"));

            await File.WriteAllTextAsync(
                Path.Combine(consumerRoot, "Consumer.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="TedToolkit.CodeAnalysis"
                                      Version="2026.7.29"
                                      PrivateAssets="all" />
                  </ItemGroup>
                  <Target Name="RecordResolvedAnalyzers"
                          AfterTargets="TedToolkitCodeAnalysisValidateSonarAnalyzer"
                          BeforeTargets="CoreCompile">
                    <WriteLinesToFile File="$(MSBuildProjectDirectory)/resolved-analyzers.txt"
                                      Lines="@(Analyzer->'%(FullPath)')"
                                      Overwrite="true" />
                  </Target>
                </Project>
                """);
            await File.WriteAllTextAsync(
                Path.Combine(consumerRoot, "Sample.cs"),
                """
                namespace Consumer;

                public static class Sample
                {
                    public static void Run()
                    {
                        // TODO Remove this placeholder.
                    }
                }
                """);

            var nugetConfigPath = Path.Combine(fixtureRoot, "NuGet.Config");
            new XDocument(
                new XElement(
                    "configuration",
                    new XElement(
                        "packageSources",
                        new XElement("clear"),
                        new XElement(
                            "add",
                            new XAttribute("key", "candidate"),
                            new XAttribute("value", candidateSource)),
                        new XElement(
                            "add",
                            new XAttribute("key", "upstream"),
                            new XAttribute("value", upstreamSource))),
                    new XElement(
                        "packageSourceMapping",
                        new XElement(
                            "packageSource",
                            new XAttribute("key", "candidate"),
                            new XElement(
                                "package",
                                new XAttribute("pattern", "TedToolkit.*"))),
                        new XElement(
                            "packageSource",
                            new XAttribute("key", "upstream"),
                            new XElement(
                                "package",
                                new XAttribute(
                                    "pattern",
                                    "SonarAnalyzer.CSharp"))))))
                .Save(nugetConfigPath);

            var restoreResult = await RunDotNetAsync(
                consumerRoot,
                "restore",
                "Consumer.csproj",
                "--configfile",
                nugetConfigPath,
                "--packages",
                packagesRoot,
                "--no-http-cache");
            await Assert.That(restoreResult.ExitCode).IsEqualTo(0);
            await Assert.That(restoreResult.Output).DoesNotContain("http://");
            await Assert.That(restoreResult.Output).DoesNotContain("https://");

            var restoredSonarPath = Path.Combine(
                packagesRoot,
                "sonaranalyzer.csharp",
                "10.23.0.137933",
                "analyzers",
                "SonarAnalyzer.CSharp.dll");
            var restoredSonarHash = Convert.ToHexString(
                    SHA256.HashData(
                        await File.ReadAllBytesAsync(restoredSonarPath)))
                .ToLowerInvariant();
            await Assert.That(restoredSonarHash)
                .IsEqualTo(
                    "5a6476ae9a310021c9a5fa38160bd0d6297211d74613253f8b4d83ce56258289");

            await WriteSeverityAsync(consumerRoot, "warning");
            var warningResult = await RebuildConsumerAsync(consumerRoot);
            EnsureSucceeded(warningResult);
            await Assert.That(warningResult.Output).Contains("warning S1135");

            var analyzers = (await File.ReadAllLinesAsync(
                    Path.Combine(consumerRoot, "resolved-analyzers.txt")))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();
            var sonarAnalyzers = analyzers.Where(path =>
                    path.EndsWith(
                        "/sonaranalyzer.csharp/10.23.0.137933/analyzers/SonarAnalyzer.CSharp.dll",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (sonarAnalyzers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one Sonar analyzer but found {sonarAnalyzers.Length}:"
                    + $"{Environment.NewLine}{string.Join(Environment.NewLine, analyzers)}");
            }
            await Assert.That(analyzers.Any(path =>
                    path.Contains(
                        "/analyzers/dotnet/roslyn4.7/cs/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(analyzers.Any(path =>
                    path.Contains(
                        "/analyzers/dotnet/roslyn3.8/cs/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsFalse();

            var roslyn38Result = await RebuildConsumerAsync(
                consumerRoot,
                "roslyn3.8");
            EnsureSucceeded(roslyn38Result);
            var roslyn38Analyzers = (await File.ReadAllLinesAsync(
                    Path.Combine(consumerRoot, "resolved-analyzers.txt")))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();
            await Assert.That(roslyn38Analyzers.Any(path =>
                    path.Contains(
                        "/analyzers/dotnet/roslyn3.8/cs/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(roslyn38Analyzers.Any(path =>
                    path.Contains(
                        "/analyzers/dotnet/roslyn4.7/cs/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsFalse();

            await WriteSeverityAsync(consumerRoot, "none");
            var noneResult = await RebuildConsumerAsync(consumerRoot);
            EnsureSucceeded(noneResult);
            await Assert.That(noneResult.Output).DoesNotContain("S1135");

            await WriteSeverityAsync(consumerRoot, "error");
            var errorResult = await RebuildConsumerAsync(consumerRoot);
            await Assert.That(errorResult.ExitCode).IsEqualTo(1);
            await Assert.That(errorResult.Output).Contains("error S1135");

            await WriteSeverityAsync(consumerRoot, "none");
            File.Delete(restoredSonarPath);
            var missingDependencyResult = await RebuildConsumerAsync(consumerRoot);
            await Assert.That(missingDependencyResult.ExitCode).IsEqualTo(1);
            await Assert.That(missingDependencyResult.Output)
                .Contains(
                    "TedToolkit.CodeAnalysis requires SonarAnalyzer.CSharp 10.23.0.137933");
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    private static Task WriteSeverityAsync(
        string consumerRoot,
        string severity)
    {
        return File.WriteAllTextAsync(
            Path.Combine(consumerRoot, ".editorconfig"),
            $"""
             root = true

             [*.cs]
             dotnet_diagnostic.S1135.severity = {severity}
             """);
    }

    private static Task<ProcessResult> RebuildConsumerAsync(
        string consumerRoot,
        string? compilerApiVersion = null)
    {
        var arguments = new List<string>
        {
            "build",
            "Consumer.csproj",
            "--configuration",
            "Release",
            "--no-restore",
            "--target",
            "Rebuild",
            "--verbosity",
            "minimal",
        };

        if (compilerApiVersion is not null)
        {
            arguments.Add($"-p:CompilerApiVersion={compilerApiVersion}");
        }

        return RunDotNetAsync(
            consumerRoot,
            arguments.ToArray());
    }

    private static void EnsureSucceeded(ProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Output);
        }
    }

    private static string ReadPackageFolder(string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(assetsPath));

        return document.RootElement
            .GetProperty("packageFolders")
            .EnumerateObject()
            .Single()
            .Name;
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
