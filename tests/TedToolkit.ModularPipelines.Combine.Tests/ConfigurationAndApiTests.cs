using Microsoft.Extensions.Configuration;

using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Execution;
using TedToolkit.ModularPipelines.Combine.Internal;

namespace TedToolkit.ModularPipelines.Combine.Tests;

internal sealed class ConfigurationAndApiTests
{
    [Test]
    public async Task Should_preserve_issue_closing_directives_once()
    {
        var merged = IssueClosingDirectiveMerger.Merge(
            "Generated body\n\nCloses #7",
            "Old text\nCloses #7\nFixes owner/repo#9\nnot a directive",
            preserve: true);

        await Assert.That(merged)
            .IsEqualTo(
                "Generated body\n\nCloses #7\n\nFixes owner/repo#9");
    }

    /// <summary>
    /// 验证严格配置接受逻辑凭据引用并绑定显式配置。
    /// </summary>
    [Test]
    public async Task Should_bind_composed_non_secret_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TedToolkit:Pipeline:ExecutionPolicy"] = "Manual",
                ["TedToolkit:Pipeline:ManualProfile"] = "Publish",
                ["TedToolkit:Pipeline:Actions:PushNuGetPackages"] = "true",
                ["TedToolkit:Pipeline:NuGetPush:Source"] = "https://nuget.example/v3/index.json",
                ["TedToolkit:Pipeline:NuGetPush:CredentialReference"] = "NUGET_API_KEY",
            })
            .Build();

        var result = PipelineConfigurationLoader.Load(configuration);

        await Assert.That(result.Pipeline.ManualProfile)
            .IsEqualTo(PipelineProfile.Publish);
        await Assert.That(result.Pipeline.NuGetPush!.CredentialReference)
            .IsEqualTo("NUGET_API_KEY");
    }

    /// <summary>
    /// 验证未知路径与内联秘密仅报告安全配置路径。
    /// </summary>
    [Test]
    [Arguments("TedToolkit:Pipeline:Unknown", "value")]
    [Arguments("TedToolkit:Pipeline:NuGetPush:ApiKey", "do-not-echo")]
    public async Task Should_reject_unknown_or_inline_secret_configuration(
        string path,
        string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [path] = value,
            })
            .Build();

        var exception = await Assert.That(() =>
                PipelineConfigurationLoader.Load(configuration))
            .Throws<InvalidDataException>();

        await Assert.That(exception!.Message).Contains(path);
        await Assert.That(exception.Message).DoesNotContain(value);
    }

    /// <summary>
    /// 验证公开签名与程序集依赖不泄漏平台 SDK 或通知实现。
    /// </summary>
    [Test]
    public async Task Should_not_expose_provider_sdk_types_from_public_api()
    {
        var assembly = typeof(StandardPipelineExtensions).Assembly;
        var forbiddenAssemblies = new[]
        {
            "Octokit",
            "NGitLab",
            "GitLabApiClient",
            "Slack",
            "Feishu",
            "TextCopy",
            "Gemini",
        };
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        await Assert.That(references.Any(reference =>
                forbiddenAssemblies.Any(token => reference.Contains(
                    token,
                    StringComparison.OrdinalIgnoreCase))))
            .IsFalse();
    }
}
