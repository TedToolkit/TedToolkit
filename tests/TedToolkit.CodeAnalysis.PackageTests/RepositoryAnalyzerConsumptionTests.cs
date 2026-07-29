using System.Diagnostics;

namespace TedToolkit.CodeAnalysis.PackageTests;

internal sealed class RepositoryAnalyzerConsumptionTests
{
    /// <summary>
    /// 验证仓库源码项目通过显式私有引用继续加载候选分析器集合且不会形成包依赖。
    /// </summary>
    [Test]
    public async Task Should_load_repository_analyzers_through_explicit_private_references()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.CodeAnalysis.RepositoryTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            var evidencePath = Path.Combine(
                fixtureRoot,
                "resolved-analyzers.txt");
            var targetPath = Path.Combine(
                fixtureRoot,
                "RecordAnalyzers.targets");
            await File.WriteAllTextAsync(
                targetPath,
                """
                <Project>
                  <Target Name="RecordRepositoryAnalyzers"
                          DependsOnTargets="ResolveReferences">
                    <WriteLinesToFile File="$(AnalyzerEvidencePath)"
                                      Lines="@(Analyzer->'%(FullPath)')"
                                      Overwrite="true" />
                  </Target>
                </Project>
                """);

            var result = await RunDotNetAsync(
                repositoryRoot,
                "msbuild",
                Path.Combine(
                    repositoryRoot,
                    "TedToolkit.ModularPipelines",
                    "TedToolkit.ModularPipelines.csproj"),
                "-target:RecordRepositoryAnalyzers",
                "-property:Configuration=Release",
                $"-property:CustomAfterMicrosoftCommonTargets={targetPath}",
                $"-property:AnalyzerEvidencePath={evidencePath}");

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.Output);
            }

            var analyzers = (await File.ReadAllLinesAsync(evidencePath))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            await Assert.That(analyzers.Any(path =>
                    path.Contains(
                        "/roslynator.analyzers/4.15.0/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(analyzers.Any(path =>
                    path.Contains(
                        "/stylecop.analyzers.unstable/1.2.0.556/",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(analyzers.Any(path =>
                    path.EndsWith(
                        "/sonaranalyzer.csharp/10.23.0.137933/analyzers/SonarAnalyzer.CSharp.dll",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(analyzers.Any(path =>
                    path.EndsWith(
                        "/TedToolkit.CodeAnalysis.dll",
                        StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
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
