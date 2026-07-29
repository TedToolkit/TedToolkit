using System.Net;
using System.Security.Cryptography;
using System.Text;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.GitHub.Tests;

internal sealed class GitHubProviderTests
{
    /// <summary>
    /// 验证 push before revision 从受限事件文件读取，而非网络查询。
    /// </summary>
    [Test]
    [NotInParallel("ProcessEnvironment")]
    public async Task Should_read_before_revision_from_event_file()
    {
        var path = Path.GetTempFileName();
        var previousPath = Environment.GetEnvironmentVariable(
            "GITHUB_EVENT_PATH");
        var previousBefore = Environment.GetEnvironmentVariable(
            "GITHUB_EVENT_BEFORE");

        try
        {
            await File.WriteAllTextAsync(
                path,
                """{"before":"REVISION"}"""
                    .Replace(
                        "REVISION",
                        new string('a', 40),
                        StringComparison.Ordinal));
            Environment.SetEnvironmentVariable("GITHUB_EVENT_BEFORE", null);
            Environment.SetEnvironmentVariable("GITHUB_EVENT_PATH", path);

            await Assert.That(
                    GitHubActionsEnvironment.ReadBeforeRevision())
                .IsEqualTo(new string('a', 40));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "GITHUB_EVENT_PATH",
                previousPath);
            Environment.SetEnvironmentVariable(
                "GITHUB_EVENT_BEFORE",
                previousBefore);
            File.Delete(path);
        }
    }

    /// <summary>
    /// 验证显式 Enterprise Web/API 地址与仓库身份独立保留。
    /// </summary>
    [Test]
    public async Task Should_prefer_explicit_enterprise_connection()
    {
        var resolved = ProviderConnectionResolver.ResolveGitHub(new()
        {
            InstanceUrl = new("https://github.example/prefix"),
            ApiUrl = new("https://api.example/github/v3"),
            Owner = "owner",
            Repository = "repo",
            AuthenticationMode = GitHubAuthenticationMode.Token,
            CredentialReference = "GH_TOKEN",
        });

        await Assert.That(resolved.InstanceUrl.AbsoluteUri)
            .IsEqualTo("https://github.example/prefix");
        await Assert.That(resolved.ApiUrl.AbsoluteUri)
            .IsEqualTo("https://api.example/github/v3");
        await Assert.That(resolved.Owner).IsEqualTo("owner");
    }

    /// <summary>
    /// 验证前缀保留且动态路径段只编码一次。
    /// </summary>
    [Test]
    public async Task Should_preserve_prefix_and_encode_dynamic_segments()
    {
        var uri = ProviderUri.Append(
            new Uri("https://api.example/root/v3"),
            "repos",
            "owner name",
            "repo/part");

        await Assert.That(uri.AbsoluteUri)
            .IsEqualTo(
                "https://api.example/root/v3/repos/owner%20name/repo%2Fpart");
    }

    /// <summary>
    /// 验证内容相同的开放 PR 被复用且凭据只进入 API 请求头。
    /// </summary>
    [Test]
    public async Task Should_reuse_equal_open_pull_request()
    {
        var transport = new RecordingTransport();
        transport.Enqueue(
            HttpStatusCode.OK,
            """
            [{
              "number": 7,
              "title": "Merge",
              "body": "Body",
              "draft": false,
              "html_url": "https://github.example/o/r/pull/7",
              "labels": [{"name":"ready"}]
            }]
            """);
        var provider = CreateProvider(transport);
        var result = await provider.CreateOrUpdateChangeRequestAsync(
            new()
            {
                SourceBranch = "feature",
                TargetBranch = "development",
                Title = "Merge",
                Body = "Body",
                Labels = ["ready"],
            },
            CancellationToken.None);

        await Assert.That(result.Disposition)
            .IsEqualTo(OperationDisposition.Reused);
        await Assert.That(transport.Requests.Count).IsEqualTo(1);
        await Assert.That(transport.Requests[0].Authorization)
            .IsEqualTo("Bearer secret");
        await Assert.That(transport.Requests[0].Uri.AbsoluteUri)
            .DoesNotContain("secret");
    }

    /// <summary>
    /// 验证 Release-only 路径按精确 revision 建 tag、建草稿再发布。
    /// </summary>
    [Test]
    public async Task Should_finalize_release_without_artifacts()
    {
        var transport = new RecordingTransport();
        transport.Enqueue(HttpStatusCode.NotFound, "{}");
        transport.Enqueue(HttpStatusCode.Created, "{}");
        transport.Enqueue(HttpStatusCode.NotFound, "{}");
        transport.Enqueue(
            HttpStatusCode.Created,
            ReleaseJson(draft: true));
        transport.Enqueue(
            HttpStatusCode.OK,
            ReleaseJson(draft: false));
        var provider = CreateProvider(transport);
        var result = await provider.FinalizeReleaseAsync(
            new()
            {
                Version = "1.2.3",
                TagName = "v1.2.3",
                TargetRevision = new string('a', 40),
                Title = "Release 1.2.3",
                Body = "Notes",
            },
            CancellationToken.None);

        await Assert.That(result.Release.IsDraft).IsFalse();
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(transport.Requests.Select(request =>
                $"{request.Method}:{request.Uri.AbsolutePath}"))
            .IsEquivalentTo(new[]
            {
                "GET:/api/v3/repos/o/r/git/ref/tags/v1.2.3",
                "POST:/api/v3/repos/o/r/git/refs",
                "GET:/api/v3/repos/o/r/releases/tags/v1.2.3",
                "POST:/api/v3/repos/o/r/releases",
                "PATCH:/api/v3/repos/o/r/releases/11",
            });
        await Assert.That(transport.Requests[1].Body)
            .Contains(new string('a', 40));
    }

    /// <summary>
    /// 验证复用 annotated tag 时解析到其最终 commit，而非比较 tag-object SHA。
    /// </summary>
    [Test]
    public async Task Should_peel_existing_annotated_tag_to_exact_revision()
    {
        var revision = new string('a', 40);
        var tagObject = new string('b', 40);
        var transport = new RecordingTransport();
        transport.Enqueue(
            HttpStatusCode.OK,
            """{"object":{"type":"tag","sha":"TAG_OBJECT"}}"""
                .Replace("TAG_OBJECT", tagObject, StringComparison.Ordinal));
        transport.Enqueue(
            HttpStatusCode.OK,
            """{"object":{"type":"commit","sha":"REVISION"}}"""
                .Replace("REVISION", revision, StringComparison.Ordinal));
        transport.Enqueue(HttpStatusCode.OK, ReleaseJson(draft: false));
        var provider = CreateProvider(transport);

        var result = await provider.FinalizeReleaseAsync(
            new()
            {
                Version = "1.2.3",
                TagName = "v1.2.3",
                TargetRevision = revision,
                Title = "Release 1.2.3",
                Body = "Notes",
            },
            CancellationToken.None);

        await Assert.That(result.Release.Disposition)
            .IsEqualTo(OperationDisposition.Reused);
        await Assert.That(transport.Requests.Select(request =>
                $"{request.Method}:{request.Uri.AbsolutePath}"))
            .IsEquivalentTo(new[]
            {
                "GET:/api/v3/repos/o/r/git/ref/tags/v1.2.3",
                $"GET:/api/v3/repos/o/r/git/tags/{tagObject}",
                "GET:/api/v3/repos/o/r/releases/tags/v1.2.3",
            });
    }

    /// <summary>
    /// 验证 github.com 的已认证 upload_url 可流式上传，且凭据不进入 URL。
    /// </summary>
    [Test]
    public async Task Should_upload_to_trusted_github_hypermedia_origin()
    {
        var revision = new string('a', 40);
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "asset");
        var bytes = await File.ReadAllBytesAsync(filePath);
        var artifact = new PipelineArtifact
        {
            RelativePath = Path.GetFileName(filePath),
            Kind = PipelineArtifactKind.PublishArchive,
            LogicalName = "linux-x64",
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Size = bytes.Length,
        };
        var transport = new RecordingTransport();
        transport.Enqueue(
            HttpStatusCode.OK,
            """{"object":{"type":"commit","sha":"REVISION"}}"""
                .Replace("REVISION", revision, StringComparison.Ordinal));
        transport.Enqueue(
            HttpStatusCode.OK,
            ReleaseJson(
                draft: false,
                uploadUrl:
                    "https://uploads.github.com/repos/o/r/releases/11/assets{?name,label}"));
        transport.Enqueue(HttpStatusCode.OK, "[]");
        transport.Enqueue(
            HttpStatusCode.Created,
            """
            {
              "name": "FILE_NAME",
              "size": FILE_SIZE,
              "browser_download_url": "https://github.com/o/r/releases/download/v1.2.3/asset",
              "content_type": "application/octet-stream"
            }
            """
                .Replace(
                    "FILE_NAME",
                    Path.GetFileName(filePath),
                    StringComparison.Ordinal)
                .Replace(
                    "FILE_SIZE",
                    bytes.Length.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal));
        var provider = new GitHubRepositoryProvider(
            new(
                new("https://github.com"),
                new("https://api.github.com"),
                "o",
                "r",
                GitHubAuthenticationMode.Token,
                "GH_TOKEN",
                TimeSpan.FromSeconds(30)),
            "secret",
            transport);

        try
        {
            var result = await provider.PublishReleaseAsync(
                new()
                {
                    Version = "1.2.3",
                    TagName = "v1.2.3",
                    TargetRevision = revision,
                    Title = "Release 1.2.3",
                    Body = "Notes",
                    Artifacts = [artifact],
                },
                new DirectoryInfo(Path.GetDirectoryName(filePath)!),
                CancellationToken.None);

            await Assert.That(result.Artifacts.Count).IsEqualTo(1);
            await Assert.That(transport.Requests[^1].Uri.Host)
                .IsEqualTo("uploads.github.com");
            await Assert.That(transport.Requests[^1].Authorization)
                .IsEqualTo("Bearer secret");
            await Assert.That(transport.Requests[^1].Uri.AbsoluteUri)
                .DoesNotContain("secret");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static GitHubRepositoryProvider CreateProvider(
        IProviderHttpTransport transport)
    {
        return new(
            new(
                new("https://github.example"),
                new("https://api.example/api/v3"),
                "o",
                "r",
                GitHubAuthenticationMode.Token,
                "GH_TOKEN",
                TimeSpan.FromSeconds(30)),
            "secret",
            transport);
    }

    private static string ReleaseJson(
        bool draft,
        string uploadUrl =
            "https://api.example/api/v3/repos/o/r/releases/11/assets{?name,label}")
    {
        return $$"""
            {
              "id": 11,
              "tag_name": "v1.2.3",
              "name": "Release 1.2.3",
              "body": "Notes",
              "draft": {{draft.ToString().ToLowerInvariant()}},
              "prerelease": false,
              "html_url": "https://github.example/o/r/releases/tag/v1.2.3",
              "upload_url": "{{uploadUrl}}"
            }
            """;
    }

    private sealed class RecordingTransport : IProviderHttpTransport
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses =
            new();

        public List<RecordedRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body)
        {
            _responses.Enqueue((status, body));
        }

        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _ = timeout;
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(
                request.Method.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                body));
            var response = _responses.Dequeue();
            return new(response.Status)
            {
                Content = new StringContent(
                    response.Body,
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private sealed record RecordedRequest(
        string Method,
        Uri Uri,
        string? Authorization,
        string Body);
}