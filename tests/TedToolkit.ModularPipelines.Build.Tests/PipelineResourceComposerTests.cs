using System.Text;

using TedToolkit.ModularPipelines.Build.Resources;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class PipelineResourceComposerTests
{
    /// <summary>
    /// 验证默认资源仅从嵌入基线解析且不会写入仓库文件。
    /// </summary>
    [Test]
    public async Task Should_resolve_embedded_resources_without_writing_by_default()
    {
        using var root = TemporaryDirectory.Create();
        var composer = new PipelineResourceComposer();
        var options = new EmbeddedResourceOptions();

        var document = await composer.GetEditorConfigAsync(root.Directory, options);
        var write = await composer.WriteEditorConfigAsync(root.Directory, options);

        await Assert.That(document.Source).IsEqualTo(PipelineResourceSource.EmbeddedBase);
        await Assert.That(document.Content.EndsWith('\n')).IsTrue();
        await Assert.That(document.Sha256).Length().IsEqualTo(64);
        await Assert.That(write.Written).IsFalse();
        await Assert.That(write.TargetFile.Exists).IsFalse();
    }

    /// <summary>
    /// 验证覆盖层按固定空行和 LF 规则组成，并仅在显式启用时原子写入。
    /// </summary>
    [Test]
    public async Task Should_compose_overlay_and_write_only_when_enabled()
    {
        using var root = TemporaryDirectory.Create();
        var overlayPath = Path.Combine(root.Directory.FullName, "overlay.txt");
        await File.WriteAllTextAsync(
            overlayPath,
            "custom = true\r\n",
            new UTF8Encoding(false));
        var composer = new PipelineResourceComposer();
        var options = new EmbeddedResourceOptions
        {
            EditorConfigOverlayPath = "overlay.txt",
            WriteEditorConfig = true,
        };

        var first = await composer.WriteEditorConfigAsync(root.Directory, options);
        var second = await composer.WriteEditorConfigAsync(root.Directory, options);

        await Assert.That(first.Document.Source)
            .IsEqualTo(PipelineResourceSource.EmbeddedBaseWithOverlay);
        await Assert.That(first.Document.Content).EndsWith("\n\ncustom = true\n");
        await Assert.That(first.Written).IsTrue();
        await Assert.That(second.Written).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(first.TargetFile.FullName))
            .IsEqualTo(first.Document.Content);
    }

    /// <summary>
    /// 验证资源替换与覆盖层冲突、路径逃逸和无效 UTF-8 都会失败。
    /// </summary>
    [Test]
    public async Task Should_reject_unsafe_or_invalid_resource_overrides()
    {
        using var root = TemporaryDirectory.Create();
        var invalidPath = Path.Combine(root.Directory.FullName, "invalid.txt");
        await File.WriteAllBytesAsync(invalidPath, [0xC3, 0x28]);
        var composer = new PipelineResourceComposer();

        await Assert.That(async () => await composer.GetEditorConfigAsync(
                root.Directory,
                new EmbeddedResourceOptions
                {
                    EditorConfigOverlayPath = "invalid.txt",
                }))
            .Throws<InvalidDataException>();

        await Assert.That(async () => await composer.GetEditorConfigAsync(
                root.Directory,
                new EmbeddedResourceOptions
                {
                    EditorConfigOverlayPath = "invalid.txt",
                    EditorConfigReplacementPath = "invalid.txt",
                }))
            .Throws<InvalidDataException>();

        await Assert.That(async () => await composer.GetEditorConfigAsync(
                root.Directory,
                new EmbeddedResourceOptions
                {
                    EditorConfigReplacementPath = Path.Combine(
                        root.Directory.Parent!.FullName,
                        "outside.txt"),
                }))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证嵌入的编辑器与提交消息基线精确匹配迁移前 TedToolkit 资源的规范化文本。
    /// </summary>
    [Test]
    public async Task Should_match_the_normalized_pre_migration_tedtoolkit_resources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var composer = new PipelineResourceComposer();
        var editor = await composer.GetEditorConfigAsync(
            repositoryRoot,
            new EmbeddedResourceOptions());
        var commit = await composer.GetCommitMessageInstructionsAsync(
            repositoryRoot,
            new EmbeddedResourceOptions());
        var expectedEditor = Normalize(await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot.FullName, ".editorconfig")));
        var expectedCommit = Normalize(await File.ReadAllTextAsync(
            Path.Combine(
                repositoryRoot.FullName,
                "PromptLibrary",
                "CommitMessage.md")));

        await Assert.That(editor.Content).IsEqualTo(expectedEditor);
        await Assert.That(commit.Content).IsEqualTo(expectedCommit);
    }

    /// <summary>
    /// 验证提交与变更请求覆盖层保持独立，且不会发生跨种类规则泄漏。
    /// </summary>
    [Test]
    public async Task Should_keep_commit_and_change_request_overlays_independent()
    {
        using var root = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "commit.overlay"),
            "commit-only-rule");
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "request.overlay"),
            "request-only-rule");
        var composer = new PipelineResourceComposer();
        var options = new EmbeddedResourceOptions
        {
            CommitMessageOverlayPath = "commit.overlay",
            ChangeRequestOverlayPath = "request.overlay",
        };

        var commit = await composer.GetCommitMessageInstructionsAsync(
            root.Directory,
            options);
        var request = await composer.GetChangeRequestInstructionsAsync(
            root.Directory,
            options);

        await Assert.That(commit.Content).Contains("commit-only-rule");
        await Assert.That(commit.Content).DoesNotContain("request-only-rule");
        await Assert.That(request.Content).Contains("request-only-rule");
        await Assert.That(request.Content).DoesNotContain("commit-only-rule");
    }

    /// <summary>
    /// 验证 NUL 与超过 1 MiB 的资源覆盖均在读取阶段被拒绝。
    /// </summary>
    [Test]
    public async Task Should_reject_nul_and_oversized_resource_content()
    {
        using var root = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "nul.txt"),
            "before\0after");
        await File.WriteAllTextAsync(
            Path.Combine(root.Directory.FullName, "large.txt"),
            new string('x', (1024 * 1024) + 1));
        var composer = new PipelineResourceComposer();

        await Assert.That(async () =>
                await composer.GetEditorConfigAsync(
                    root.Directory,
                    new EmbeddedResourceOptions
                    {
                        EditorConfigReplacementPath = "nul.txt",
                    }))
            .Throws<InvalidDataException>();
        await Assert.That(async () =>
                await composer.GetEditorConfigAsync(
                    root.Directory,
                    new EmbeddedResourceOptions
                    {
                        EditorConfigReplacementPath = "large.txt",
                    }))
            .Throws<InvalidDataException>();
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

    private static string Normalize(string content)
    {
        return string.Concat(
            content.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .TrimEnd('\n'),
            "\n");
    }
}