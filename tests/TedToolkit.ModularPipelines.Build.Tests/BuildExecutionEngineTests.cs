using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Resources;
using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class BuildExecutionEngineTests
{
    /// <summary>
    /// 验证 clean 失败会阻止对应 build，并仍在版本还原后写出失败清单。
    /// </summary>
    [Test]
    public async Task Should_gate_build_after_clean_failure_and_write_failed_manifest()
    {
        using var fixture = await BuildFixture.CreateAsync();
        var executor = new RecordingCommandExecutor(request =>
            request.Arguments[0] == "clean"
                ? new CommandResult(1, "", "", false, false)
                : Success());
        var engine = fixture.CreateEngine(
            BuildExecutionProfile.Validate,
            executor,
            buildTargets:
            [
                new BuildTarget
                {
                    Name = "library",
                    File = fixture.Project,
                    CleanBeforeBuild = true,
                },
            ]);

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Failed);
        await Assert.That(executor.Requests.Count(request =>
                request.Arguments[0] == "clean"))
            .IsEqualTo(1);
        await Assert.That(executor.Requests.Any(request =>
                request.Arguments[0] == "build"))
            .IsFalse();
        await Assert.That(File.Exists(Path.Combine(
                fixture.ArtifactRoot.FullName,
                "pipeline-artifacts.v1.json")))
            .IsTrue();
        await Assert.That(result.Targets[0].FailureLogPath).IsNotNull();
        var failureLog = await File.ReadAllTextAsync(Path.Combine(
            fixture.ArtifactRoot.FullName,
            result.Targets[0].FailureLogPath!.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        await Assert.That(failureLog).DoesNotContain("secret");
        await Assert.That(failureLog).DoesNotContain("StandardOutput");
    }

    /// <summary>
    /// 验证 MTP run 使用隔离 TRX 参数并聚合 Passed、Failed、Skipped 结果。
    /// </summary>
    [Test]
    public async Task Should_run_mtp_with_fresh_trx_and_aggregate_outcomes()
    {
        using var fixture = await BuildFixture.CreateAsync();
        var executor = new RecordingCommandExecutor(request =>
        {
            if (request.Arguments[0] == "run")
            {
                var arguments = request.Arguments.ToList();
                var resultDirectory = arguments[
                    arguments.IndexOf("--report-trx-directory") + 1];
                Directory.CreateDirectory(resultDirectory);
                File.WriteAllText(
                    Path.Combine(resultDirectory, "tests.trx"),
                    """
                    <TestRun>
                      <Results>
                        <UnitTestResult outcome="Passed" />
                        <UnitTestResult outcome="NotExecuted" />
                      </Results>
                    </TestRun>
                    """);
            }

            return Success();
        });
        var engine = fixture.CreateEngine(
            BuildExecutionProfile.Validate,
            executor,
            testTargets:
            [
                new TestTarget
                {
                    Name = "tests",
                    File = fixture.Project,
                    Command = DotNetTestCommand.MicrosoftTestingPlatformRun,
                },
            ]);

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Succeeded);
        await Assert.That(result.Tests).Count().IsEqualTo(1);
        await Assert.That(result.Tests[0].Passed).IsEqualTo(1);
        await Assert.That(result.Tests[0].Skipped).IsEqualTo(1);
        await Assert.That(result.Tests[0].Failed).IsEqualTo(0);
        var run = executor.Requests.Single(request =>
            request.Arguments[0] == "run");
        await Assert.That(run.Arguments).Contains("--");
        await Assert.That(run.Arguments).Contains("--report-trx");
    }

    /// <summary>
    /// 验证相同发布输入生成固定时间戳、排序稳定且哈希一致的原子归档。
    /// </summary>
    [Test]
    public async Task Should_create_deterministic_publish_archives()
    {
        var hashes = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            using var fixture = await BuildFixture.CreateAsync();
            var executor = new RecordingCommandExecutor(request =>
            {
                if (request.Arguments[0] == "publish")
                {
                    var arguments = request.Arguments.ToList();
                    var output = arguments[arguments.IndexOf("--output") + 1];
                    Directory.CreateDirectory(Path.Combine(output, "nested"));
                    File.WriteAllText(Path.Combine(output, "z.txt"), "z");
                    File.WriteAllText(
                        Path.Combine(output, "nested", "a.txt"),
                        "a");
                }

                return Success();
            });
            var engine = fixture.CreateEngine(
                BuildExecutionProfile.Publish,
                executor,
                publishTargets:
                [
                    new DotnetPublishTarget
                    {
                        Name = "app",
                        ArtifactName = "application",
                        File = fixture.Project,
                        Framework = "net10.0",
                    },
                ]);

            var result = await engine.RunAsync(CancellationToken.None);
            var artifact = result.Artifacts.Single(item =>
                item.Kind == PipelineArtifactKind.PublishArchive);

            await Assert.That(artifact.LogicalName).IsEqualTo("application");
            await Assert.That(artifact.TargetFramework).IsEqualTo("net10.0");
            hashes.Add(artifact.Sha256);
        }

        await Assert.That(hashes.Distinct()).Count().IsEqualTo(1);
    }

    /// <summary>
    /// 验证 MTP test 与 VSTest 使用各自声明的 CLI、分隔符和 TRX logger 形状。
    /// </summary>
    [Test]
    [Arguments(DotNetTestCommand.MicrosoftTestingPlatformTest)]
    [Arguments(DotNetTestCommand.VSTest)]
    public async Task Should_use_the_declared_test_command_shape(
        DotNetTestCommand command)
    {
        using var fixture = await BuildFixture.CreateAsync();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root.FullName, "global.json"),
            command == DotNetTestCommand.MicrosoftTestingPlatformTest
                ? """{"test":{"runner":"Microsoft.Testing.Platform"}}"""
                : """{"test":{"runner":"VSTest"}}""");
        var executor = new RecordingCommandExecutor(request =>
        {
            if (request.Arguments[0] == "test")
            {
                var arguments = request.Arguments.ToList();
                var resultDirectory = arguments[
                    arguments.IndexOf("--results-directory") + 1];
                Directory.CreateDirectory(resultDirectory);
                File.WriteAllText(
                    Path.Combine(resultDirectory, "result.trx"),
                    """
                    <TestRun><Results>
                      <UnitTestResult outcome="Passed" />
                    </Results></TestRun>
                    """);
            }

            return Success();
        });
        var engine = fixture.CreateEngine(
            BuildExecutionProfile.Validate,
            executor,
            testTargets:
            [
                new TestTarget
                {
                    Name = "tests",
                    File = fixture.Project,
                    Command = command,
                },
            ]);

        var result = await engine.RunAsync(CancellationToken.None);
        var request = executor.Requests.Single(item =>
            item.Arguments[0] == "test");

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Succeeded);
        await Assert.That(request.Arguments.Contains("--project"))
            .IsEqualTo(command
                == DotNetTestCommand.MicrosoftTestingPlatformTest);
        await Assert.That(request.Arguments.Contains("--logger"))
            .IsEqualTo(command == DotNetTestCommand.VSTest);
        await Assert.That(request.Arguments).Contains(
            command == DotNetTestCommand.VSTest
                ? "trx;LogFileName=tests.trx"
                : "--report-trx");
    }

    /// <summary>
    /// 验证陈旧、格式错误、零测试与非零退出的 TRX 场景均产生失败结果。
    /// </summary>
    [Test]
    public async Task Should_reject_stale_malformed_empty_or_nonzero_test_results()
    {
        var cases = new[]
        {
            (Content: "<TestRun><Results><UnitTestResult outcome=\"Passed\" /></Results></TestRun>",
                Stale: true,
                ExitCode: 0),
            (Content: "<not-trx>",
                Stale: false,
                ExitCode: 0),
            (Content: "<TestRun><Results /></TestRun>",
                Stale: false,
                ExitCode: 0),
            (Content: "<TestRun><Results><UnitTestResult outcome=\"Passed\" /></Results></TestRun>",
                Stale: false,
                ExitCode: 1),
        };

        foreach (var testCase in cases)
        {
            using var fixture = await BuildFixture.CreateAsync();
            var executor = new RecordingCommandExecutor(request =>
            {
                if (request.Arguments[0] == "run")
                {
                    var arguments = request.Arguments.ToList();
                    var resultDirectory = arguments[
                        arguments.IndexOf("--report-trx-directory") + 1];
                    Directory.CreateDirectory(resultDirectory);
                    var path = Path.Combine(resultDirectory, "result.trx");
                    File.WriteAllText(path, testCase.Content);

                    if (testCase.Stale)
                    {
                        File.SetLastWriteTimeUtc(
                            path,
                            DateTime.UtcNow.AddMinutes(-10));
                    }
                }

                return new CommandResult(
                    testCase.ExitCode,
                    "",
                    "",
                    false,
                    false);
            });
            var engine = fixture.CreateEngine(
                BuildExecutionProfile.Validate,
                executor,
                testTargets:
                [
                    new TestTarget
                    {
                        Name = "tests",
                        File = fixture.Project,
                        Command =
                            DotNetTestCommand.MicrosoftTestingPlatformRun,
                    },
                ]);

            var result = await engine.RunAsync(CancellationToken.None);

            await Assert.That(result.Status)
                .IsEqualTo(PipelineRunStatus.Failed);
            await Assert.That(result.Tests[0].Succeeded).IsFalse();
        }
    }

    /// <summary>
    /// 验证 WorkingTreeTracked、HeadTracked 与 All 精确选择各自的格式化路径范围。
    /// </summary>
    [Test]
    [Arguments(FormatScope.WorkingTreeTracked, 1)]
    [Arguments(FormatScope.HeadTracked, 2)]
    [Arguments(FormatScope.All, 0)]
    public async Task Should_apply_the_declared_format_scope(
        FormatScope scope,
        int expectedIncludedPaths)
    {
        using var fixture = await BuildFixture.CreateAsync();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root.FullName, "working.cs"),
            "class Working {}");
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root.FullName, "staged.cs"),
            "class Staged {}");
        var executor = new RecordingCommandExecutor(request =>
            request.FileName == "git"
                ? new CommandResult(
                    0,
                    scope == FormatScope.WorkingTreeTracked
                        ? "working.cs\0"
                        : "working.cs\0staged.cs\0",
                    "",
                    false,
                    false)
                : Success());
        var engine = fixture.CreateEngine(
            BuildExecutionProfile.LocalBuild,
            executor,
            formatTargets:
            [
                new FormatTarget
                {
                    File = fixture.Project,
                    Scope = scope,
                },
            ],
            options: new BuildOptions
            {
                RunFormat = true,
            });

        var result = await engine.RunAsync(CancellationToken.None);
        var format = executor.Requests.Single(request =>
            request.FileName == "dotnet"
            && request.Arguments[0] == "format");
        var formatArguments = format.Arguments.ToList();
        var includeIndex = formatArguments.IndexOf("--include");
        var includedPaths = includeIndex < 0
            ? 0
            : formatArguments.Count - includeIndex - 1;

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Succeeded);
        await Assert.That(includedPaths).IsEqualTo(expectedIncludedPaths);
        await Assert.That(executor.Requests.Any(request =>
                request.FileName == "git"))
            .IsEqualTo(scope != FormatScope.All);
    }

    /// <summary>
    /// 验证描述生成仅在显式启用时调用，并规范化输出而不持久化到清单。
    /// </summary>
    [Test]
    public async Task Should_generate_only_when_enabled_and_keep_output_in_process()
    {
        using var fixture = await BuildFixture.CreateAsync();
        var generator = new RecordingDescriptionGenerator(
            new ChangeDescription
            {
                Subject = "  concise subject  ",
                Body = "line one\r\nline two",
            });
        var executor = new RecordingCommandExecutor(request =>
            request.Arguments[0] == "diff"
                ? new CommandResult(
                    0,
                    "safe textual diff",
                    "",
                    false,
                    false)
                : request.Arguments[0] == "status"
                    ? new CommandResult(
                        0,
                        " M tracked.cs\0?? untracked.cs\0",
                        "",
                        false,
                        false)
                    : Success());
        var engine = fixture.CreateEngine(
            BuildExecutionProfile.LocalBuild,
            executor,
            options: new BuildOptions
            {
                GenerateChangeDescriptions = true,
            },
            descriptionGenerator: generator);

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(generator.Requests).Count().IsEqualTo(1);
        await Assert.That(generator.Requests[0].ChangedPaths)
            .IsEquivalentTo(new[] { "tracked.cs", "untracked.cs", });
        await Assert.That(result.GeneratedDescription!.Subject)
            .IsEqualTo("concise subject");
        await Assert.That(result.GeneratedDescription.Body)
            .IsEqualTo("line one\nline two");
        await Assert.That(Directory.EnumerateFiles(
                fixture.ArtifactRoot.FullName,
                "*",
                SearchOption.AllDirectories)
            .Any(path => File.ReadAllText(path)
                .Contains("concise subject", StringComparison.Ordinal)))
            .IsFalse();
    }

    /// <summary>
    /// 验证无生成器时正常跳过，非法输出分别遵循 Continue 与 FailPipeline。
    /// </summary>
    [Test]
    public async Task Should_apply_description_absence_and_failure_modes()
    {
        using (var fixture = await BuildFixture.CreateAsync())
        {
            var executor = new RecordingCommandExecutor(_ => Success());
            var engine = fixture.CreateEngine(
                BuildExecutionProfile.LocalBuild,
                executor,
                options: new BuildOptions
                {
                    GenerateChangeDescriptions = true,
                });
            var result = await engine.RunAsync(CancellationToken.None);

            await Assert.That(result.Status)
                .IsEqualTo(PipelineRunStatus.Succeeded);
            await Assert.That(executor.Requests).IsEmpty();
        }

        foreach (var failureMode in new[]
                 {
                     ChangeDescriptionFailureMode.Continue,
                     ChangeDescriptionFailureMode.FailPipeline,
                 })
        {
            using var fixture = await BuildFixture.CreateAsync();
            var executor = new RecordingCommandExecutor(request =>
                request.Arguments[0] == "diff"
                    ? new CommandResult(0, "diff", "", false, false)
                    : request.Arguments[0] == "status"
                        ? new CommandResult(
                            0,
                            " M tracked.cs\0",
                            "",
                            false,
                            false)
                        : Success());
            var engine = fixture.CreateEngine(
                BuildExecutionProfile.LocalBuild,
                executor,
                options: new BuildOptions
                {
                    GenerateChangeDescriptions = true,
                    ChangeDescriptionFailureMode = failureMode,
                },
                descriptionGenerator: new RecordingDescriptionGenerator(
                    new ChangeDescription
                    {
                        Subject = "invalid\nsubject",
                    }));

            var result = await engine.RunAsync(CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(
                failureMode == ChangeDescriptionFailureMode.Continue
                    ? PipelineRunStatus.Succeeded
                    : PipelineRunStatus.Failed);
            await Assert.That(result.GeneratedDescription).IsNull();
        }
    }

    /// <summary>
    /// 验证真实 SDK pack 在事务期间写入日历版本，并在包、程序集、清单完成后逐字节恢复源文件。
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task Should_stamp_real_compiled_and_package_versions_then_restore(
        CancellationToken cancellationToken)
    {
        using var root = TemporaryDirectory.Create();
        var projectPath = Path.Combine(
            root.Directory.FullName,
            "Fixture.Package.csproj");
        var sourcePath = Path.Combine(
            root.Directory.FullName,
            "Library.cs");
        var propsPath = Path.Combine(
            root.Directory.FullName,
            "Directory.Build.props");
        var artifactRoot = root.Directory.CreateSubdirectory("output");
        await File.WriteAllTextAsync(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackageId>Fixture.Package</PackageId>
                <IncludeSymbols>true</IncludeSymbols>
                <SymbolPackageFormat>snupkg</SymbolPackageFormat>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            sourcePath,
            "namespace Fixture; public sealed class Library;");
        await File.WriteAllTextAsync(
            propsPath,
            """
            <Project>
              <PropertyGroup>
                <Version>0.1.0</Version>
              </PropertyGroup>
            </Project>
            """);
        var originalProps = await File.ReadAllBytesAsync(propsPath);
        var process = new ProcessCommandExecutor();
        var restore = await process.ExecuteAsync(
            new CommandRequest(
                "dotnet",
                [
                    "restore",
                    projectPath,
                    "--ignore-failed-sources",
                    "-p:NuGetAudit=false",
                ],
                root.Directory.FullName,
                TimeSpan.FromMinutes(2)),
            cancellationToken);
        await Assert.That(restore.ExitCode).IsEqualTo(0);
        var recording = new GitNeutralProcessExecutor(
            process,
            new FileInfo(propsPath),
            "2026.7.29.4");
        var inputs = new BuildInputs
        {
            RootDirectory = root.Directory,
            ArtifactRootDirectory = artifactRoot,
            SourceRevision = "fixture-revision",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = new FileInfo(propsPath),
                TargetVersion = "2026.7.29.4",
                RecoveryFile = new FileInfo(Path.Combine(
                    root.Directory.FullName,
                    ".version-recovery.json")),
            },
            PackTargets =
            [
                new PackTarget
                {
                    Name = "fixture-package",
                    File = new FileInfo(projectPath),
                    PackageVersion = "2026.7.29.4",
                    Arguments = ["--no-restore",],
                },
            ],
        };
        var engine = new BuildExecutionEngine(
            BuildExecutionProfile.Pack,
            inputs,
            new BuildOptions(),
            recording,
            new PipelineResourceComposer(),
            new PipelineArtifactManifestStore());

        var result = await engine.RunAsync(cancellationToken);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Succeeded);
        await Assert.That(recording.SawStampedVersion).IsTrue();
        await Assert.That(recording.Requests.Single(request =>
                request.Arguments[0] == "pack").Arguments)
            .DoesNotContain("--no-build");
        await Assert.That(await File.ReadAllBytesAsync(propsPath))
            .IsEquivalentTo(originalProps);
        await Assert.That(File.Exists(Path.Combine(
                root.Directory.FullName,
                ".version-recovery.json")))
            .IsFalse();
        var packageArtifact = result.Artifacts.Single(artifact =>
            artifact.Kind == PipelineArtifactKind.NuGetPackage);
        await Assert.That(packageArtifact.PackageVersion)
            .IsEqualTo("2026.7.29.4");
        var packagePath = Path.Combine(
            artifactRoot.FullName,
            packageArtifact.RelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        using var archive = ZipFile.OpenRead(packagePath);
        var assemblyEntry = archive.Entries.Single(entry =>
            entry.FullName.EndsWith(
                "/Fixture.Package.dll",
                StringComparison.Ordinal));
        using var assemblyBytes = new MemoryStream();
        await using (var entryStream = assemblyEntry.Open())
        {
            await entryStream.CopyToAsync(assemblyBytes);
        }

        assemblyBytes.Position = 0;
        var loadContext = new AssemblyLoadContext(
            "FixturePackageMetadata",
            isCollectible: true);
        var assembly = loadContext.LoadFromStream(assemblyBytes);
        await Assert.That(assembly.GetName().Version!.ToString())
            .IsEqualTo("2026.7.29.4");
        await Assert.That(assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()!
                .Version)
            .IsEqualTo("2026.7.29.4");
        await Assert.That(assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion.Split('+')[0])
            .IsEqualTo("2026.7.29.4");
        loadContext.Unload();
        var manifest = await new PipelineArtifactManifestStore().ReadAsync(
            root.Directory,
            new FileInfo(Path.Combine(
                artifactRoot.FullName,
                "pipeline-artifacts.v1.json")),
            new ArtifactValidationOptions
            {
                ExpectedSourceRevision = "fixture-revision",
            });
        await Assert.That(manifest.PackageVersion)
            .IsEqualTo("2026.7.29.4");
    }

    /// <summary>
    /// 验证包内程序集版本与临时日历版本不一致时，运行失败并移除不可发布包。
    /// </summary>
    [Test]
    public async Task Should_reject_mismatched_compiled_package_versions()
    {
        using var fixture = await BuildFixture.CreateAsync();
        var packageId = Path.GetFileNameWithoutExtension(
            typeof(BuildExecutionEngineTests).Assembly.Location);
        var executor = new RecordingCommandExecutor(request =>
        {
            if (request.Arguments[0] == "pack")
            {
                var arguments = request.Arguments.ToList();
                var output = arguments[arguments.IndexOf("--output") + 1];
                var version = arguments.Single(argument =>
                        argument.StartsWith(
                            "-p:PackageVersion=",
                            StringComparison.Ordinal))
                    ["-p:PackageVersion=".Length..];
                Directory.CreateDirectory(output);
                using var archive = ZipFile.Open(
                    Path.Combine(output, $"{packageId}.{version}.nupkg"),
                    ZipArchiveMode.Create);
                var nuspec = archive.CreateEntry($"{packageId}.nuspec");
                using (var writer = new StreamWriter(nuspec.Open()))
                {
                    writer.Write(
                        $"<package><metadata><id>{packageId}</id><version>{version}</version></metadata></package>");
                }

                archive.CreateEntryFromFile(
                    typeof(BuildExecutionEngineTests).Assembly.Location,
                    $"lib/net10.0/{packageId}.dll");
            }

            return Success();
        });
        var inputs = new BuildInputs
        {
            RootDirectory = fixture.Root,
            ArtifactRootDirectory = fixture.ArtifactRoot,
            SourceRevision = "abc123",
            MsBuildVersion = new TemporaryMsBuildVersionOptions
            {
                TargetFile = fixture.Props,
                TargetVersion = "2026.7.29.2",
                RecoveryFile = new FileInfo(Path.Combine(
                    fixture.Root.FullName,
                    ".version-recovery.json")),
            },
            PackTargets =
            [
                new PackTarget
                {
                    Name = "mismatched",
                    File = fixture.Project,
                    PackageVersion = "2026.7.29.2",
                },
            ],
        };
        var engine = new BuildExecutionEngine(
            BuildExecutionProfile.Pack,
            inputs,
            new BuildOptions(),
            executor,
            new PipelineResourceComposer(),
            new PipelineArtifactManifestStore());

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Failed);
        await Assert.That(result.Failures.Any(failure =>
                failure.Code == "PACKAGE_OUTPUT_INVALID"))
            .IsTrue();
        await Assert.That(result.Artifacts.Any(artifact =>
                artifact.Kind == PipelineArtifactKind.NuGetPackage))
            .IsFalse();
        await Assert.That(await File.ReadAllTextAsync(
                fixture.Props.FullName))
            .Contains("<Version>1.0.0</Version>");
    }

    /// <summary>
    /// 验证协作取消后仍以独立清理边界恢复版本并写出 Failed 清单。
    /// </summary>
    [Test]
    public async Task Should_restore_and_write_failed_manifest_after_cancellation()
    {
        using var fixture = await BuildFixture.CreateAsync();
        var originalBytes = await File.ReadAllBytesAsync(
            fixture.Props.FullName);
        var executor = new RecordingCommandExecutor(request =>
        {
            if (request.Arguments[0] == "pack")
            {
                throw new OperationCanceledException();
            }

            return Success();
        });
        var engine = CreateVersionedPackEngine(
            fixture,
            executor,
            "2026.7.29.3");

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Failed);
        await Assert.That(result.Failures.Any(failure =>
                failure.Code == "BUILD_CANCELLED"))
            .IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(
                fixture.Props.FullName))
            .IsEquivalentTo(originalBytes);
        await Assert.That(File.Exists(Path.Combine(
                fixture.ArtifactRoot.FullName,
                "pipeline-artifacts.v1.json")))
            .IsTrue();
    }

    /// <summary>
    /// 验证事务中的第三方文件修改导致恢复失败，且不会覆盖冲突字节或产生成功结果。
    /// </summary>
    [Test]
    public async Task Should_fail_without_overwriting_when_restoration_conflicts()
    {
        using var fixture = await BuildFixture.CreateAsync();
        const string conflictingContent = "<Project>conflicting</Project>";
        var executor = new RecordingCommandExecutor(request =>
        {
            if (request.Arguments[0] == "pack")
            {
                File.WriteAllText(
                    fixture.Props.FullName,
                    conflictingContent);
                return new CommandResult(1, "", "", false, false);
            }

            return Success();
        });
        var engine = CreateVersionedPackEngine(
            fixture,
            executor,
            "2026.7.29.3");

        var result = await engine.RunAsync(CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(PipelineRunStatus.Failed);
        await Assert.That(result.Failures.Any(failure =>
                failure.Code == "VERSION_RESTORE_FAILED"))
            .IsTrue();
        await Assert.That(await File.ReadAllTextAsync(
                fixture.Props.FullName))
            .IsEqualTo(conflictingContent);
        await Assert.That(File.Exists(Path.Combine(
                fixture.Root.FullName,
                ".version-recovery.json")))
            .IsTrue();
    }

    private static BuildExecutionEngine CreateVersionedPackEngine(
        BuildFixture fixture,
        ICommandExecutor executor,
        string version)
    {
        return new BuildExecutionEngine(
            BuildExecutionProfile.Pack,
            new BuildInputs
            {
                RootDirectory = fixture.Root,
                ArtifactRootDirectory = fixture.ArtifactRoot,
                SourceRevision = "abc123",
                MsBuildVersion = new TemporaryMsBuildVersionOptions
                {
                    TargetFile = fixture.Props,
                    TargetVersion = version,
                    RecoveryFile = new FileInfo(Path.Combine(
                        fixture.Root.FullName,
                        ".version-recovery.json")),
                },
                PackTargets =
                [
                    new PackTarget
                    {
                        Name = "package",
                        File = fixture.Project,
                        PackageVersion = version,
                    },
                ],
            },
            new BuildOptions(),
            executor,
            new PipelineResourceComposer(),
            new PipelineArtifactManifestStore());
    }

    private static CommandResult Success()
    {
        return new CommandResult(0, "", "", false, false);
    }

    private sealed class RecordingCommandExecutor(
        Func<CommandRequest, CommandResult> handler)
        : ICommandExecutor
    {
        public List<CommandRequest> Requests { get; } = [];

        public Task<CommandResult> ExecuteAsync(
            CommandRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class BuildFixture : IDisposable
    {
        private readonly TemporaryDirectory _root;

        private BuildFixture(
            TemporaryDirectory root,
            DirectoryInfo artifactRoot,
            FileInfo project,
            FileInfo props)
        {
            _root = root;
            ArtifactRoot = artifactRoot;
            Project = project;
            Props = props;
        }

        public DirectoryInfo ArtifactRoot { get; }

        public DirectoryInfo Root => _root.Directory;

        public FileInfo Project { get; }

        public FileInfo Props { get; }

        public static async Task<BuildFixture> CreateAsync()
        {
            var root = TemporaryDirectory.Create();
            var artifactRoot = root.Directory.CreateSubdirectory("output");
            var projectPath = Path.Combine(root.Directory.FullName, "sample.csproj");
            var propsPath = Path.Combine(
                root.Directory.FullName,
                "Directory.Build.props");
            await File.WriteAllTextAsync(projectPath, "<Project />");
            await File.WriteAllTextAsync(
                propsPath,
                "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
            return new BuildFixture(
                root,
                artifactRoot,
                new FileInfo(projectPath),
                new FileInfo(propsPath));
        }

        public BuildExecutionEngine CreateEngine(
            BuildExecutionProfile profile,
            ICommandExecutor executor,
            IReadOnlyList<BuildTarget>? buildTargets = null,
            IReadOnlyList<TestTarget>? testTargets = null,
            IReadOnlyList<DotnetPublishTarget>? publishTargets = null,
            IReadOnlyList<FormatTarget>? formatTargets = null,
            IReadOnlyList<PackTarget>? packTargets = null,
            BuildOptions? options = null,
            IChangeDescriptionGenerator? descriptionGenerator = null)
        {
            var inputs = new BuildInputs
            {
                RootDirectory = _root.Directory,
                ArtifactRootDirectory = ArtifactRoot,
                SourceRevision = "abc123",
                MsBuildVersion = new TemporaryMsBuildVersionOptions
                {
                    TargetFile = Props,
                    RecoveryFile = new FileInfo(Path.Combine(
                        _root.Directory.FullName,
                        ".version-recovery.json")),
                },
                BuildTargets = buildTargets ?? [],
                TestTargets = testTargets ?? [],
                PublishTargets = publishTargets ?? [],
                FormatTargets = formatTargets ?? [],
                PackTargets = packTargets ?? [],
            };
            return new BuildExecutionEngine(
                profile,
                inputs,
                options ?? new BuildOptions(),
                executor,
                new PipelineResourceComposer(),
                new PipelineArtifactManifestStore(),
                descriptionGenerator);
        }

        public void Dispose()
        {
            _root.Dispose();
        }
    }

    private sealed class RecordingDescriptionGenerator(
        ChangeDescription? result)
        : IChangeDescriptionGenerator
    {
        public List<ChangeDescriptionRequest> Requests { get; } = [];

        public Task<ChangeDescription?> GenerateAsync(
            ChangeDescriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class GitNeutralProcessExecutor(
        ProcessCommandExecutor process,
        FileInfo versionFile,
        string targetVersion)
        : ICommandExecutor
    {
        public List<CommandRequest> Requests { get; } = [];

        public bool SawStampedVersion { get; private set; }

        public async Task<CommandResult> ExecuteAsync(
            CommandRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.FileName == "git")
            {
                return new CommandResult(0, "", "", false, false);
            }

            SawStampedVersion |= (await File.ReadAllTextAsync(
                    versionFile.FullName,
                    cancellationToken))
                .Contains(
                    $"<Version>{targetVersion}</Version>",
                    StringComparison.Ordinal);
            return await process.ExecuteAsync(request, cancellationToken);
        }
    }
}