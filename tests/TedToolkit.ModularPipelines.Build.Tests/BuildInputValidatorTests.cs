using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Internal;
using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class BuildInputValidatorTests
{
    /// <summary>
    /// 验证安全的显式根目录、恢复配置和目标可以在发布配置中通过。
    /// </summary>
    [Test]
    public async Task Should_accept_safe_explicit_publish_inputs()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var project = await CreateFileAsync(root.Directory, "sample.csproj", "<Project />");
        var props = await CreateFileAsync(
            root.Directory,
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = props,
                TargetVersion = "2026.7.29",
                RecoveryFile = new FileInfo(
                    Path.Combine(root.Directory.FullName, ".version-recovery.json")),
            },
            PackTargets =
            [
                new PackTarget
                {
                    Name = "package",
                    File = project,
                    PackageVersion = "2026.7.29",
                },
            ],
            PublishTargets =
            [
                new DotnetPublishTarget
                {
                    Name = "app",
                    ArtifactName = "application",
                    File = project,
                    RuntimeIdentifier = "win-x64",
                    SelfContained = false,
                },
            ],
        };

        BuildInputValidator.Validate(
            BuildExecutionProfile.Publish,
            inputs,
            new BuildOptions());

        await Assert.That(inputs.RootDirectory.Exists).IsTrue();
    }

    /// <summary>
    /// 验证根目录相等、重复名称、保留参数和无恢复配置在命令执行前被拒绝。
    /// </summary>
    [Test]
    public async Task Should_reject_unsafe_roots_targets_and_owned_switches()
    {
        using var root = TemporaryDirectory.Create();
        var project = await CreateFileAsync(root.Directory, "sample.csproj", "<Project />");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = root.Directory,
            SourceRevision = "abc123",
            BuildTargets =
            [
                new BuildTarget
                {
                    Name = "same",
                    File = project,
                    Arguments = ["--configuration=Debug"],
                },
                new BuildTarget
                {
                    Name = "same",
                    File = project,
                },
            ],
        };

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                inputs,
                new BuildOptions()))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证普通仓库写入仅允许 LocalBuild，版本写入仅允许 Pack 或 Publish。
    /// </summary>
    [Test]
    public async Task Should_enforce_the_profile_write_matrix()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var props = await CreateFileAsync(
            root.Directory,
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var baseInputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = props,
                RecoveryFile = new FileInfo(
                    Path.Combine(root.Directory.FullName, ".version-recovery.json")),
            },
        };

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                baseInputs,
                new BuildOptions
                {
                    Resources = new()
                    {
                        WriteEditorConfig = true,
                    },
                }))
            .Throws<InvalidDataException>();

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                baseInputs with
                {
                    MsBuildVersion = baseInputs.MsBuildVersion with
                    {
                        TargetVersion = "2026.7.29",
                    },
                },
                new BuildOptions()))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证不存在但安全的制品目录可被接受，而非法名称、枚举和配置在命令前失败。
    /// </summary>
    [Test]
    public async Task Should_validate_names_enums_and_absent_artifact_roots()
    {
        using var root = TemporaryDirectory.Create();
        var project = await CreateFileAsync(
            root.Directory,
            "sample.csproj",
            "<Project />");
        var props = await CreateFileAsync(
            root.Directory,
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = new DirectoryInfo(Path.Combine(
                root.Directory.FullName,
                "not-created-yet")),
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = props,
                RecoveryFile = new FileInfo(Path.Combine(
                    root.Directory.FullName,
                    ".version-recovery.json")),
            },
            BuildTargets =
            [
                new BuildTarget
                {
                    Name = "library",
                    File = project,
                },
            ],
        };

        BuildInputValidator.Validate(
            BuildExecutionProfile.Validate,
            inputs,
            new BuildOptions());

        await Assert.That(inputs.ArtifactRootDirectory.Exists).IsFalse();
        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                inputs with
                {
                    BuildTargets =
                    [
                        inputs.BuildTargets[0] with
                        {
                            Name = "../escape",
                        },
                    ],
                },
                new BuildOptions()))
            .Throws<InvalidDataException>();
        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                inputs,
                new BuildOptions
                {
                    ChangeDescriptionFailureMode =
                        (ChangeDescriptionFailureMode)999,
                }))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证 NuGet 版本按规范化值比较，混合版本和发布归档名碰撞会被拒绝。
    /// </summary>
    [Test]
    public async Task Should_normalize_pack_versions_and_reject_archive_collisions()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var project = await CreateFileAsync(
            root.Directory,
            "sample.csproj",
            "<Project />");
        var props = await CreateFileAsync(
            root.Directory,
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = props,
                TargetVersion = "2026.7.29",
                RecoveryFile = new FileInfo(Path.Combine(
                    root.Directory.FullName,
                    ".version-recovery.json")),
            },
            PackTargets =
            [
                new PackTarget
                {
                    Name = "first",
                    File = project,
                    PackageVersion = "2026.7.29.0",
                },
                new PackTarget
                {
                    Name = "second",
                    File = project,
                    PackageVersion = "2026.7.29",
                },
            ],
        };

        BuildInputValidator.Validate(
            BuildExecutionProfile.Pack,
            inputs,
            new BuildOptions());

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Pack,
                inputs with
                {
                    PackTargets =
                    [
                        inputs.PackTargets[0],
                        inputs.PackTargets[1] with
                        {
                            PackageVersion = "2026.7.29.1",
                        },
                    ],
                },
                new BuildOptions()))
            .Throws<InvalidDataException>();
        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Publish,
                inputs with
                {
                    PublishTargets =
                    [
                        new DotnetPublishTarget
                        {
                            Name = "publish-one",
                            ArtifactName = "application",
                            File = project,
                            Framework = "net10.0",
                        },
                        new DotnetPublishTarget
                        {
                            Name = "publish-two",
                            ArtifactName = "application-net10.0",
                            File = project,
                        },
                    ],
                },
                new BuildOptions()))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证分支约定、MTP runner 选择和配置文件所有权组合受到严格检查。
    /// </summary>
    [Test]
    public async Task Should_validate_conventions_and_test_runner_selection()
    {
        using var root = TemporaryDirectory.Create();
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        var project = await CreateFileAsync(
            root.Directory,
            "sample.csproj",
            "<Project />");
        var props = await CreateFileAsync(
            root.Directory,
            "Directory.Build.props",
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = props,
                RecoveryFile = new FileInfo(Path.Combine(
                    root.Directory.FullName,
                    ".version-recovery.json")),
            },
            TestTargets =
            [
                new TestTarget
                {
                    Name = "tests",
                    File = project,
                    Command =
                        DotNetTestCommand.MicrosoftTestingPlatformTest,
                },
            ],
        };

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                inputs,
                new BuildOptions()))
            .Throws<InvalidDataException>();

        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "global.json"),
            """{"test":{"runner":"Microsoft.Testing.Platform"}}""");
        BuildInputValidator.Validate(
            BuildExecutionProfile.Validate,
            inputs,
            new BuildOptions());

        await Assert.That(() => BuildInputValidator.Validate(
                BuildExecutionProfile.Validate,
                inputs,
                new BuildOptions
                {
                    Conventions = new PipelineConventionOptions
                    {
                        MainBranch = "bad..branch",
                    },
                }))
            .Throws<InvalidDataException>();
    }

    private static async Task<FileInfo> CreateFileAsync(
        DirectoryInfo root,
        string name,
        string content)
    {
        var path = Path.Combine(root.FullName, name);
        await File.WriteAllTextAsync(path, content);
        return new FileInfo(path);
    }
}