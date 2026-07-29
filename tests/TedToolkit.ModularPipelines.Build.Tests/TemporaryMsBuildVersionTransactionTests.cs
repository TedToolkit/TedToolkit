using System.Diagnostics;
using System.Security.Cryptography;

using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class TemporaryMsBuildVersionTransactionTests
{
    /// <summary>
    /// 验证事务期间写入目标版本，并在完成后逐字节恢复原始 MSBuild 文件。
    /// </summary>
    [Test]
    public async Task Should_stamp_and_restore_the_msbuild_file_byte_for_byte()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var targetPath = Path.Combine(root.Directory.FullName, "Directory.Build.props");
        var recoveryPath = Path.Combine(root.Directory.FullName, ".tedtoolkit-version-recovery.json");
        var original = "\uFEFF<Project>\r\n  <PropertyGroup>\r\n    <Version>1.0.0</Version>\r\n  </PropertyGroup>\r\n</Project>";
        await File.WriteAllTextAsync(targetPath, original);
        var originalBytes = await File.ReadAllBytesAsync(targetPath);
        var options = new TemporaryMsBuildVersionOptions
        {
            TargetFile = new FileInfo(targetPath),
            PropertyName = "Version",
            TargetVersion = "2026.7.29.4",
            RecoveryFile = new FileInfo(recoveryPath),
        };

        await using (var transaction = await TemporaryMsBuildVersionTransaction.BeginAsync(
                         root.Directory,
                         artifactRoot,
                         options))
        {
            var temporary = await File.ReadAllTextAsync(targetPath);

            await Assert.That(temporary).Contains("<Version>2026.7.29.4</Version>");
            await Assert.That(File.Exists(recoveryPath)).IsTrue();
            await transaction.RestoreAsync(CancellationToken.None);
        }

        await Assert.That(await File.ReadAllBytesAsync(targetPath))
            .IsEquivalentTo(originalBytes);
        await Assert.That(File.Exists(recoveryPath)).IsFalse();
    }

    /// <summary>
    /// 验证后续活动运行可从遗留恢复状态还原目标文件。
    /// </summary>
    [Test]
    public async Task Should_recover_an_interrupted_matching_transaction()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var targetPath = Path.Combine(root.Directory.FullName, "Directory.Build.props");
        var recoveryPath = Path.Combine(root.Directory.FullName, ".tedtoolkit-version-recovery.json");
        await File.WriteAllTextAsync(
            targetPath,
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var originalBytes = await File.ReadAllBytesAsync(targetPath);
        var options = new TemporaryMsBuildVersionOptions
        {
            TargetFile = new FileInfo(targetPath),
            PropertyName = "Version",
            TargetVersion = "2026.7.29",
            RecoveryFile = new FileInfo(recoveryPath),
        };
        var transaction = await TemporaryMsBuildVersionTransaction.BeginAsync(
            root.Directory,
            artifactRoot,
            options);
        await transaction.AbandonForTestAsync();

        await TemporaryMsBuildVersionTransaction.RecoverAsync(
            root.Directory,
            artifactRoot,
            options with { TargetVersion = null },
            CancellationToken.None);

        await Assert.That(await File.ReadAllBytesAsync(targetPath))
            .IsEquivalentTo(originalBytes);
        await Assert.That(File.Exists(recoveryPath)).IsFalse();
    }

    /// <summary>
    /// 验证目标文件与恢复记录均不匹配时拒绝覆盖用户修改。
    /// </summary>
    [Test]
    public async Task Should_not_overwrite_a_conflicting_target_during_recovery()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var targetPath = Path.Combine(root.Directory.FullName, "Directory.Build.props");
        var recoveryPath = Path.Combine(root.Directory.FullName, ".tedtoolkit-version-recovery.json");
        await File.WriteAllTextAsync(
            targetPath,
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var options = new TemporaryMsBuildVersionOptions
        {
            TargetFile = new FileInfo(targetPath),
            PropertyName = "Version",
            TargetVersion = "2026.7.29",
            RecoveryFile = new FileInfo(recoveryPath),
        };
        var transaction = await TemporaryMsBuildVersionTransaction.BeginAsync(
            root.Directory,
            artifactRoot,
            options);
        await transaction.AbandonForTestAsync();
        await File.WriteAllTextAsync(targetPath, "<Project />");
        var conflictHash = await HashAsync(targetPath);

        await Assert.That(() => TemporaryMsBuildVersionTransaction.RecoverAsync(
                root.Directory,
                artifactRoot,
                options with { TargetVersion = null },
                CancellationToken.None))
            .Throws<InvalidDataException>();

        await Assert.That(await HashAsync(targetPath)).IsEqualTo(conflictHash);
        await Assert.That(File.Exists(recoveryPath)).IsTrue();
    }

    /// <summary>
    /// 验证仓库只忽略精确的根级恢复文件，不会扩大到子目录或相邻名称。
    /// </summary>
    [Test]
    public async Task Should_ignore_only_the_exact_root_recovery_file()
    {
        var repositoryRoot = FindRepositoryRoot();

        await Assert.That(await IsIgnoredAsync(
                repositoryRoot,
                ".tedtoolkit-release-version.recovery"))
            .IsTrue();
        await Assert.That(await IsIgnoredAsync(
                repositoryRoot,
                "nested/.tedtoolkit-release-version.recovery"))
            .IsFalse();
        await Assert.That(await IsIgnoredAsync(
                repositoryRoot,
                ".tedtoolkit-release-version.recovery.extra"))
            .IsFalse();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "The repository root could not be located.");
    }

    private static async Task<bool> IsIgnoredAsync(
        DirectoryInfo repositoryRoot,
        string relativePath)
    {
        using var process = new Process()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = repositoryRoot.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("check-ignore");
        process.StartInfo.ArgumentList.Add("--quiet");
        process.StartInfo.ArgumentList.Add("--no-index");
        process.StartInfo.ArgumentList.Add(relativePath);
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream));
    }
}