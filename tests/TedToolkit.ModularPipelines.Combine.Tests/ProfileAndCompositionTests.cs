using ModularPipelines;

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Execution;
using TedToolkit.ModularPipelines.Combine.Internal;

namespace TedToolkit.ModularPipelines.Combine.Tests;

internal sealed class ProfileAndCompositionTests
{
    /// <summary>
    /// 验证 GitLab parent_pipeline 精确映射为可复用调用。
    /// </summary>
    [Test]
    [NotInParallel("ProcessEnvironment")]
    public async Task Should_map_gitlab_parent_pipeline()
    {
        var values = new Dictionary<string, string?>
        {
            ["GITLAB_CI"] = Environment.GetEnvironmentVariable("GITLAB_CI"),
            ["GITHUB_ACTIONS"] =
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            ["CI_PIPELINE_SOURCE"] =
                Environment.GetEnvironmentVariable("CI_PIPELINE_SOURCE"),
            ["CI_COMMIT_REF_NAME"] =
                Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME"),
        };

        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
            Environment.SetEnvironmentVariable("GITLAB_CI", "true");
            Environment.SetEnvironmentVariable(
                "CI_PIPELINE_SOURCE",
                "parent_pipeline");
            Environment.SetEnvironmentVariable(
                "CI_COMMIT_REF_NAME",
                "main");
            var resolved = StandardProfileResolver.Resolve(
                new CombineOptions(),
                new PipelineConventionOptions());

            await Assert.That(resolved.Profile)
                .IsEqualTo(PipelineProfile.Publish);
        }
        finally
        {
            foreach (var value in values)
            {
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }
    }

    /// <summary>
    /// 验证显式上下文严格映射本地、主分支、普通分支与标签推送。
    /// </summary>
    [Test]
    [Arguments(PipelineTriggerKind.Local, false, null, PipelineProfile.LocalBuild)]
    [Arguments(PipelineTriggerKind.Push, true, "main", PipelineProfile.Publish)]
    [Arguments(PipelineTriggerKind.Push, true, "feature/x", PipelineProfile.Message)]
    [Arguments(PipelineTriggerKind.Push, true, null, PipelineProfile.None)]
    public async Task Should_resolve_standard_profile_from_neutral_context(
        PipelineTriggerKind trigger,
        bool isCi,
        string? branch,
        PipelineProfile expected)
    {
        var options = new CombineOptions
        {
            ExecutionContext = new()
            {
                Trigger = trigger,
                IsCi = isCi,
                Branch = branch,
            },
        };

        var resolved = StandardProfileResolver.Resolve(
            options,
            new PipelineConventionOptions());

        await Assert.That(resolved.Profile).IsEqualTo(expected);
    }

    /// <summary>
    /// 验证手动策略只采用调用方选择，不从环境猜测。
    /// </summary>
    [Test]
    [Arguments(PipelineProfile.LocalBuild)]
    [Arguments(PipelineProfile.Validate)]
    [Arguments(PipelineProfile.Pack)]
    [Arguments(PipelineProfile.Publish)]
    [Arguments(PipelineProfile.Message)]
    [Arguments(PipelineProfile.None)]
    public async Task Should_accept_every_explicit_manual_profile(
        PipelineProfile profile)
    {
        var resolved = StandardProfileResolver.Resolve(
            new CombineOptions
            {
                ExecutionPolicy = PipelineExecutionPolicy.Manual,
                ManualProfile = profile,
            },
            new PipelineConventionOptions());

        await Assert.That(resolved.Profile).IsEqualTo(profile);
    }

    /// <summary>
    /// 验证禁止的动作在任何扩展回调与 Build 命令前失败。
    /// </summary>
    [Test]
    public async Task Should_reject_forbidden_action_before_extension_callback()
    {
        using var root = TemporaryDirectory.Create();
        var callbacks = 0;
        var builder = Pipeline.CreateBuilder();
        var registration = new PipelineModuleRegistration(
            new HashSet<PipelineProfile> { PipelineProfile.Validate },
            PipelineExtensionKind.CombineHook,
            false,
            _ => callbacks++);
        var options = new CombineOptions
        {
            ExecutionPolicy = PipelineExecutionPolicy.Manual,
            ManualProfile = PipelineProfile.Validate,
            Actions = new()
            {
                CreateRelease = true,
            },
        };

        await Assert.That(() => builder.AddStandardPipeline(
                root.Directory,
                options,
                CreateBuildInputs(root.Directory),
                moduleRegistrations: [registration]))
            .Throws<InvalidDataException>();
        await Assert.That(callbacks).IsEqualTo(0);
    }

    /// <summary>
    /// 验证 None 不验证输入、不调用扩展，并返回同一个构建器。
    /// </summary>
    [Test]
    public async Task Should_make_none_a_zero_call_composition()
    {
        using var root = TemporaryDirectory.Create();
        var callbacks = 0;
        var builder = Pipeline.CreateBuilder();
        var registration = new PipelineModuleRegistration(
            new HashSet<PipelineProfile> { PipelineProfile.LocalBuild },
            PipelineExtensionKind.BuildStage,
            true,
            _ => callbacks++);

        var returned = builder.AddStandardPipeline(
            root.Directory,
            new CombineOptions
            {
                ExecutionPolicy = PipelineExecutionPolicy.Manual,
                ManualProfile = PipelineProfile.None,
                ArtifactManifestPath = "..\\missing.json",
            },
            buildInputs: null,
            moduleRegistrations: [registration]);

        await Assert.That(returned).IsSameReferenceAs(builder);
        await Assert.That(callbacks).IsEqualTo(0);
    }

    /// <summary>
    /// 验证 ConsumeArtifacts 与 Build 输入冲突时不会调用扩展。
    /// </summary>
    [Test]
    public async Task Should_reject_consume_conflicts_before_callbacks()
    {
        using var root = TemporaryDirectory.Create();
        var callbacks = 0;
        var builder = Pipeline.CreateBuilder();
        var registration = new PipelineModuleRegistration(
            new HashSet<PipelineProfile> { PipelineProfile.Validate },
            PipelineExtensionKind.CombineHook,
            false,
            _ => callbacks++);

        await Assert.That(() => builder.AddStandardPipeline(
                root.Directory,
                new CombineOptions
                {
                    ExecutionPolicy = PipelineExecutionPolicy.Manual,
                    ManualProfile = PipelineProfile.Validate,
                    InputMode = PipelineInputMode.ConsumeArtifacts,
                    ArtifactManifestPath = "artifacts/manifest.json",
                },
                CreateBuildInputs(root.Directory),
                moduleRegistrations: [registration]))
            .Throws<InvalidDataException>();
        await Assert.That(callbacks).IsEqualTo(0);
    }

    /// <summary>
    /// 验证启用检查点但未提供唯一可信实现时在扩展回调前失败。
    /// </summary>
    [Test]
    public async Task Should_reject_missing_checkpoint_before_callbacks()
    {
        using var root = TemporaryDirectory.Create();
        var callbacks = 0;
        var builder = Pipeline.CreateBuilder();
        var registration = new PipelineModuleRegistration(
            new HashSet<PipelineProfile> { PipelineProfile.Publish },
            PipelineExtensionKind.CombineHook,
            false,
            _ => callbacks++);

        await Assert.That(() => builder.AddStandardPipeline(
                root.Directory,
                new CombineOptions
                {
                    ExecutionPolicy = PipelineExecutionPolicy.Manual,
                    ManualProfile = PipelineProfile.Publish,
                    Actions = new()
                    {
                        PushNuGetPackages = true,
                    },
                    NuGetPush = new()
                    {
                        Source = new("https://nuget.example/v3/index.json"),
                        CredentialReference = "NUGET_KEY",
                        UsePackagePublicationCheckpoint = true,
                    },
                },
                CreateBuildInputs(root.Directory),
                moduleRegistrations: [registration]))
            .Throws<InvalidDataException>();
        await Assert.That(callbacks).IsEqualTo(0);
    }

    private static BuildInputs CreateBuildInputs(DirectoryInfo root)
    {
        return new()
        {
            RootDirectory = root,
            ArtifactRootDirectory = new(Path.Combine(
                root.FullName,
                "artifacts")),
            SourceRevision = new string('a', 40),
        };
    }
}