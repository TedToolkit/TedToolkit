namespace TedToolkit.CodeAnalysis.PackageTests;

internal sealed class AnalyzerInventoryTests
{
    private static readonly string[] ExpectedCopiedPackages =
    [
        "Roslynator.Analyzers,4.15.0",
        "Roslynator.CodeAnalysis.Analyzers,4.15.0",
        "Roslynator.CodeFixes,4.15.0",
        "Roslynator.Formatting.Analyzers,4.15.0",
        "StyleCop.Analyzers.Unstable,1.2.0.556",
    ];

    /// <summary>
    /// 验证每个复制的分析器资产都有明确的审计清单条目。
    /// </summary>
    [Test]
    public async Task Should_record_every_selected_asset_in_the_approved_inventory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var inventoryPath = Path.Combine(
            repositoryRoot,
            "TedToolkit.CodeAnalysis",
            "analyzer-assets.csv");

        await Assert.That(File.Exists(inventoryPath)).IsTrue();

        var lines = await File.ReadAllLinesAsync(inventoryPath);

        await Assert.That(lines[0])
            .IsEqualTo("PackageId,PackageVersion,License,Host,PackagePath,Sha256");

        foreach (var expectedPackage in ExpectedCopiedPackages)
        {
            await Assert.That(lines.Any(line => line.StartsWith(
                    $"{expectedPackage},",
                    StringComparison.Ordinal)))
                .IsTrue();
        }
    }

    /// <summary>
    /// 验证复制资产清单完整、无路径冲突且具有再分发证据。
    /// </summary>
    [Test]
    public async Task Should_require_license_and_notice_evidence_for_every_inventory_entry()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectRoot = Path.Combine(repositoryRoot, "TedToolkit.CodeAnalysis");
        var inventoryLines = await File.ReadAllLinesAsync(
            Path.Combine(projectRoot, "analyzer-assets.csv"));
        var noticePath = Path.Combine(projectRoot, "THIRD-PARTY-NOTICES.txt");

        await Assert.That(File.Exists(noticePath)).IsTrue();
        await Assert.That(inventoryLines).Count().IsEqualTo(72);

        var assetRows = inventoryLines
            .Skip(1)
            .Select(ParseInventoryRow)
            .ToArray();

        await Assert.That(assetRows.Select(row => row.PackagePath).Distinct(StringComparer.Ordinal))
            .Count()
            .IsEqualTo(assetRows.Length);
        await Assert.That(assetRows.All(row => row.Sha256.Length == 64
                                               && row.Sha256.All(character =>
                                                   character is >= '0' and <= '9'
                                                   or >= 'a' and <= 'f')))
            .IsTrue();
        await Assert.That(assetRows.Count(row =>
                row.PackagePath.EndsWith("CodeFixes.dll", StringComparison.Ordinal)))
            .IsEqualTo(9);

        var notices = await File.ReadAllTextAsync(noticePath);

        foreach (var expectedPackage in ExpectedCopiedPackages)
        {
            var parts = expectedPackage.Split(',');

            await Assert.That(notices).Contains(parts[0]);
            await Assert.That(notices).Contains(parts[1]);
        }

        foreach (var license in assetRows.Select(row => row.License).Distinct(StringComparer.Ordinal))
        {
            await Assert.That(notices).Contains(license);
        }
    }

    /// <summary>
    /// 验证 Sonar 仅作为精确外部依赖记录，不进入复制资产清单。
    /// </summary>
    [Test]
    public async Task Should_record_sonar_as_an_exact_external_dependency()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectRoot = Path.Combine(repositoryRoot, "TedToolkit.CodeAnalysis");
        var inventory = await File.ReadAllTextAsync(
            Path.Combine(projectRoot, "analyzer-assets.csv"));
        var dependencyLines = await File.ReadAllLinesAsync(
            Path.Combine(projectRoot, "analyzer-dependencies.csv"));

        await Assert.That(inventory).DoesNotContain("SonarAnalyzer.CSharp");
        await Assert.That(dependencyLines).Count().IsEqualTo(2);
        await Assert.That(dependencyLines[0])
            .IsEqualTo("PackageId,PackageVersion,VersionRange,License,PackagePath,Sha256,Disposition");
        await Assert.That(dependencyLines[1])
            .IsEqualTo(
                "SonarAnalyzer.CSharp,10.23.0.137933,[10.23.0.137933],Sonar-Source-Available-1.0,analyzers/SonarAnalyzer.CSharp.dll,5a6476ae9a310021c9a5fa38160bd0d6297211d74613253f8b4d83ce56258289,ExternalDependency");
    }

    private static InventoryRow ParseInventoryRow(string line)
    {
        var fields = line.Split(',');

        if (fields.Length != 6)
        {
            throw new InvalidDataException($"Invalid inventory row: {line}");
        }

        return new InventoryRow(
            fields[0],
            fields[1],
            fields[2],
            fields[3],
            fields[4],
            fields[5]);
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

        throw new InvalidOperationException("The repository root could not be located.");
    }

    private sealed record InventoryRow(
        string PackageId,
        string PackageVersion,
        string License,
        string Host,
        string PackagePath,
        string Sha256);
}
