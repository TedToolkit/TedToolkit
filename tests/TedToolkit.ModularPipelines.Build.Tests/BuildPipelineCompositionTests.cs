using System.IO.Compression;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Modules;
using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class BuildPipelineCompositionTests
{
    /// <summary>
    /// 验证 None 返回原构建器且不会读取或验证任何 Build 输入。
    /// </summary>
    [Test]
    public async Task Should_return_the_same_builder_without_work_for_none()
    {
        var builder = Pipeline.CreateBuilder();

        var returned = builder.AddBuildPipeline(
            BuildExecutionProfile.None,
            null!);

        await Assert.That(returned).IsSameReferenceAs(builder);
    }

    /// <summary>
    /// 验证 Build 程序集不引用 Combine 或任何托管平台 SDK。
    /// </summary>
    [Test]
    public async Task Should_have_a_platform_neutral_dependency_boundary()
    {
        var references = typeof(BuildPipelineExtensions)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();
        var forbidden =
            new[] { "GitHub", "GitLab", "Octokit", "NGitLab", "OpenAI", "Gemini", "TextCopy" };

        await Assert.That(references.Any(reference => forbidden.Any(token =>
                reference.Contains(token, StringComparison.OrdinalIgnoreCase))))
            .IsFalse();
    }

    /// <summary>
    /// 验证五个活动配置均通过固定模块图执行，且只产生配置允许的本地制品。
    /// </summary>
    [Test]
    [Arguments(BuildExecutionProfile.LocalBuild)]
    [Arguments(BuildExecutionProfile.Validate)]
    [Arguments(BuildExecutionProfile.Pack)]
    [Arguments(BuildExecutionProfile.Publish)]
    [Arguments(BuildExecutionProfile.Message)]
    public async Task Should_execute_the_fixed_graph_for_each_active_profile(
        BuildExecutionProfile profile)
    {
        using var root = TemporaryDirectory.Create();
        var projectPath = Path.Combine(
            root.Directory.FullName,
            "sample.csproj");
        var propsPath = Path.Combine(
            root.Directory.FullName,
            "Directory.Build.props");
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(
            propsPath,
            "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        var artifactRoot = new DirectoryInfo(
            Path.Combine(root.Directory.FullName, "custom-artifacts"));
        var executor = new CompositionCommandExecutor();
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = new FileInfo(propsPath),
                RecoveryFile = new FileInfo(Path.Combine(
                    root.Directory.FullName,
                    ".version-recovery.json")),
            },
            PackTargets = profile == BuildExecutionProfile.Pack
                ?
                [
                    new PackTarget
                    {
                        Name = "package",
                        File = new FileInfo(projectPath),
                        PackageVersion = "1.2.3.0",
                    },
                ]
                : [],
            PublishTargets = profile == BuildExecutionProfile.Publish
                ?
                [
                    new DotnetPublishTarget
                    {
                        Name = "application",
                        ArtifactName = "application",
                        File = new FileInfo(projectPath),
                    },
                ]
                : [],
        };
        var builder = Pipeline.CreateBuilder();
        var returned = builder.AddBuildPipeline(profile, inputs);
        builder.ConfigureServices((_, services) =>
        {
            services.RemoveAll<ICommandExecutor>();
            services.AddSingleton<ICommandExecutor>(executor);
        });

        await returned.ExecutePipelineAsync();

        await Assert.That(returned).IsSameReferenceAs(builder);
        await Assert.That(artifactRoot.Exists).IsTrue();
        await Assert.That(executor.Requests.Any(request =>
                request.Arguments[0] is "push" or "commit"))
            .IsFalse();
        await Assert.That(File.Exists(Path.Combine(
                artifactRoot.FullName,
                "pipeline-artifacts.v1.json")))
            .IsEqualTo(profile is BuildExecutionProfile.Validate
                or BuildExecutionProfile.Pack
                or BuildExecutionProfile.Publish);
    }

    /// <summary>
    /// 验证公开阶段、固定模块及描述模块均存在，且旧单体适配器已移除。
    /// </summary>
    [Test]
    public async Task Should_expose_the_approved_stage_extension_surface()
    {
        var assembly = typeof(BuildPipelineExtensions).Assembly;
        var requiredTypes = new[]
        {
            typeof(CleanStageModule<>),
            typeof(PrepareStageModule<>),
            typeof(CompileStageModule<>),
            typeof(CheckStageModule<>),
            typeof(CleanOutputModule),
            typeof(UpdateEditorConfigModule),
            typeof(FormatCodeModule),
            typeof(DotnetBuildModule),
            typeof(TedToolkit.ModularPipelines.Build.Modules.TestModule),
            typeof(AssertBuildTestModule),
            typeof(GenerateCommitMessageModule),
            typeof(DotnetPackModule),
            typeof(DotnetPublishModule),
            typeof(CollectPublishArtifactsModule),
            typeof(WriteArtifactManifestModule),
        };

        await Assert.That(requiredTypes.All(type => type.IsPublic)).IsTrue();
        await Assert.That(assembly.GetType(
                "TedToolkit.ModularPipelines.Build.Modules.BuildPipelineModule"))
            .IsNull();
    }

    /// <summary>
    /// 验证所有公开或受保护签名均不泄漏托管平台、具体模型或通知 SDK 类型。
    /// </summary>
    [Test]
    public async Task Should_not_expose_provider_sdk_types_from_public_api()
    {
        var forbidden = new[]
        {
            "GitHub",
            "GitLab",
            "Octokit",
            "NGitLab",
            "OpenAI",
            "Gemini",
            "Microsoft.Extensions.AI",
            "TextCopy",
            "Feishu",
            "Slack",
        };
        var exposedTypeNames = typeof(BuildPipelineExtensions).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMembers()
                .Where(member => member is System.Reflection.MethodBase
                    or System.Reflection.PropertyInfo
                    or System.Reflection.FieldInfo
                    or System.Reflection.EventInfo)
                .SelectMany(GetReferencedTypes))
            .Select(type => type.AssemblyQualifiedName ?? type.FullName ?? type.Name)
            .ToArray();

        await Assert.That(exposedTypeNames.Any(name => forbidden.Any(token =>
                name.Contains(token, StringComparison.OrdinalIgnoreCase))))
            .IsFalse();
    }

    private static IEnumerable<Type> GetReferencedTypes(
        System.Reflection.MemberInfo member)
    {
        return member switch
        {
            System.Reflection.MethodInfo method =>
            [
                method.ReturnType,
                .. method.GetParameters().Select(parameter =>
                    parameter.ParameterType),
            ],
            System.Reflection.ConstructorInfo constructor =>
            [
                .. constructor.GetParameters().Select(parameter =>
                    parameter.ParameterType),
            ],
            System.Reflection.PropertyInfo property => [property.PropertyType,],
            System.Reflection.FieldInfo field => [field.FieldType,],
            System.Reflection.EventInfo eventInfo =>
                [eventInfo.EventHandlerType ?? typeof(void),],
            _ => [],
        };
    }

    private sealed class CompositionCommandExecutor : ICommandExecutor
    {
        public List<CommandRequest> Requests { get; } = [];

        public Task<CommandResult> ExecuteAsync(
            CommandRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Arguments[0] == "pack")
            {
                CreatePackage(request.Arguments);
            }
            else if (request.Arguments[0] == "publish")
            {
                var arguments = request.Arguments.ToList();
                var output = arguments[arguments.IndexOf("--output") + 1];
                Directory.CreateDirectory(output);
                File.WriteAllText(
                    Path.Combine(output, "application.dll"),
                    "local-output");
            }

            return Task.FromResult(
                new CommandResult(0, "", "", false, false));
        }

        private static void CreatePackage(IReadOnlyList<string> arguments)
        {
            var argumentList = arguments.ToList();
            var output = argumentList[argumentList.IndexOf("--output") + 1];
            var version = arguments.Single(argument =>
                    argument.StartsWith(
                        "-p:PackageVersion=",
                        StringComparison.Ordinal))
                ["-p:PackageVersion=".Length..];
            Directory.CreateDirectory(output);
            var packagePath = Path.Combine(
                output,
                $"Sample.Package.{version}.nupkg");
            using var archive = ZipFile.Open(
                packagePath,
                ZipArchiveMode.Create);
            var entry = archive.CreateEntry("Sample.Package.nuspec");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(
                $"<package><metadata><id>Sample.Package</id><version>{version}</version></metadata></package>");
        }
    }
}