using System.Net;
using System.Security.Cryptography;
using System.Text;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.GitLab.Tests;

internal sealed class GitLabProviderTests
{
    /// <summary>
    /// 验证自建 GitLab 的 Web/API 主机与路径前缀相互独立。
    /// </summary>
    [Test]
    public async Task Should_keep_custom_instance_and_api_urls_independent()
    {
        var resolved = ProviderConnectionResolver.ResolveGitLab(new()
        {
            InstanceUrl = new("https://gitlab.example/web"),
            ApiUrl = new("https://api.example/proxy/api/v4"),
            ProjectId = 42,
            AuthenticationMode = GitLabAuthenticationMode.PrivateToken,
            CredentialReference = "GITLAB_TOKEN",
        });

        await Assert.That(resolved.InstanceUrl.AbsoluteUri)
            .IsEqualTo("https://gitlab.example/web");
        await Assert.That(resolved.ApiUrl.AbsoluteUri)
            .IsEqualTo("https://api.example/proxy/api/v4");
        await Assert.That(resolved.ProjectId).IsEqualTo(42);
    }

    /// <summary>
    /// 验证不安全 URL 在任何凭据解析与客户端调用前失败。
    /// </summary>
    [Test]
    public async Task Should_reject_insecure_or_unsafe_urls()
    {
        await Assert.That(() => ProviderConnectionResolver.ResolveGitLab(new()
        {
            InstanceUrl = new("http://gitlab.example"),
            ApiUrl = new("https://api.example/api/v4?token=x"),
            ProjectId = 42,
        }))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证内容相同的开放 MR 被复用并使用精确私有令牌头。
    /// </summary>
    [Test]
    public async Task Should_reuse_equal_open_merge_request()
    {
        var transport = new RecordingTransport();
        transport.Enqueue(
            HttpStatusCode.OK,
            """
            [{
              "iid": 9,
              "title": "Merge",
              "description": "Body",
              "draft": false,
              "web_url": "https://gitlab.example/group/repo/-/merge_requests/9",
              "labels": ["ready"]
            }]
            """);
        var provider = CreateProvider(
            transport,
            GitLabAuthenticationMode.PrivateToken);
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
        await Assert.That(transport.Requests[0].PrivateToken)
            .IsEqualTo("secret");
        await Assert.That(transport.Requests[0].Uri.AbsoluteUri)
            .DoesNotContain("secret");
    }

    /// <summary>
    /// 验证 Release-only 路径使用精确 revision 且不触发 Generic Package。
    /// </summary>
    [Test]
    public async Task Should_finalize_release_without_generic_packages()
    {
        var transport = new RecordingTransport();
        transport.Enqueue(HttpStatusCode.NotFound, "{}");
        transport.Enqueue(HttpStatusCode.Created, "{}");
        transport.Enqueue(HttpStatusCode.NotFound, "{}");
        transport.Enqueue(
            HttpStatusCode.Created,
            """
            {
              "tag_name": "v1.2.3",
              "name": "Release 1.2.3",
              "description": "Notes",
              "_links": {"self":"https://gitlab.example/g/r/-/releases/v1.2.3"},
              "assets": {"links":[]}
            }
            """);
        var provider = CreateProvider(
            transport,
            GitLabAuthenticationMode.JobToken);
        var result = await provider.FinalizeReleaseAsync(
            new()
            {
                Version = "1.2.3",
                TagName = "v1.2.3",
                TargetRevision = new string('b', 40),
                Title = "Release 1.2.3",
                Body = "Notes",
            },
            CancellationToken.None);

        await Assert.That(result.Release.TagName).IsEqualTo("v1.2.3");
        await Assert.That(transport.Requests.Any(request =>
                request.Uri.AbsolutePath.Contains(
                    "/packages/",
                    StringComparison.Ordinal)))
            .IsFalse();
        await Assert.That(transport.Requests.All(request =>
                request.JobToken == "secret"))
            .IsTrue();
        await Assert.That(transport.Requests[1].Body)
            .Contains(new string('b', 40));
    }

    /// <summary>
    /// 验证 Generic Package 查询先于流式上传，且每个动态段只编码一次。
    /// </summary>
    [Test]
    public async Task Should_query_then_stream_generic_package_with_encoded_segments()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "archive");
        var bytes = await File.ReadAllBytesAsync(filePath);
        var transport = new RecordingTransport();
        transport.Enqueue(HttpStatusCode.OK, "[]");
        transport.Enqueue(HttpStatusCode.Created, "{}");
        var provider = CreateProvider(
            transport,
            GitLabAuthenticationMode.OAuthToken);

        try
        {
            var result = await provider.PublishArtifactsAsync(
                new()
                {
                    Version = "1.2.3+meta",
                    Artifacts =
                    [
                        new PipelineArtifact
                        {
                            RelativePath = Path.GetFileName(filePath),
                            Kind = PipelineArtifactKind.PublishArchive,
                            LogicalName = "linux x64/pkg",
                            Sha256 = Convert.ToHexStringLower(
                                SHA256.HashData(bytes)),
                            Size = bytes.Length,
                        },
                    ],
                },
                new DirectoryInfo(Path.GetDirectoryName(filePath)!),
                CancellationToken.None);

            await Assert.That(result.Count).IsEqualTo(1);
            await Assert.That(transport.Requests.Select(request =>
                    request.Method))
                .IsEquivalentTo(new[] { "GET", "PUT", });
            await Assert.That(transport.Requests[1].Uri.AbsoluteUri)
                .Contains(
                    "/linux%20x64%2Fpkg/1.2.3%2Bmeta/",
                    StringComparison.Ordinal);
            await Assert.That(transport.Requests[1].Authorization)
                .IsEqualTo("Bearer secret");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static GitLabRepositoryProvider CreateProvider(
        IProviderHttpTransport transport,
        GitLabAuthenticationMode authenticationMode)
    {
        return new(
            new(
                new("https://gitlab.example"),
                new("https://api.example/proxy/api/v4"),
                42,
                authenticationMode,
                authenticationMode == GitLabAuthenticationMode.JobToken
                    ? null
                    : "GITLAB_TOKEN",
                TimeSpan.FromSeconds(30)),
            "secret",
            transport);
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
                GetHeader(request, "PRIVATE-TOKEN"),
                GetHeader(request, "JOB-TOKEN"),
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

        private static string? GetHeader(
            HttpRequestMessage request,
            string name)
        {
            return request.Headers.TryGetValues(name, out var values)
                ? values.Single()
                : null;
        }
    }

    private sealed record RecordedRequest(
        string Method,
        Uri Uri,
        string? PrivateToken,
        string? JobToken,
        string? Authorization,
        string Body);
}