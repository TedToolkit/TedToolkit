// -----------------------------------------------------------------------
// <copyright file="GitLabRepositoryProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Implements GitLab operations over the internal REST transport.
/// </summary>
/// <param name="connection">The validated non-secret connection.</param>
/// <param name="token">The resolved owning-action token.</param>
/// <param name="transport">The redirect-free transport.</param>
internal sealed class GitLabRepositoryProvider(
    ResolvedGitLabConnection connection,
    string token,
    IProviderHttpTransport transport) : IRepositoryProvider
{
    private static readonly HttpStatusCode[] OkStatus =
        [HttpStatusCode.OK,];

    private static readonly HttpStatusCode[] CreatedStatus =
        [HttpStatusCode.Created,];

    private readonly Uri _projectApi = ProviderUri.Append(
        connection.ApiUrl,
        "projects",
        connection.ProjectId.ToString(
            System.Globalization.CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public Task<RepositoryContext> GetContextAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Environment.GetEnvironmentVariable("CI_PROJECT_PATH")
                   ?? connection.ProjectId.ToString(
                       System.Globalization.CultureInfo.InvariantCulture);
        var name = path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()
            ?? path;
        return Task.FromResult<RepositoryContext>(new()
        {
            RepositoryId = connection.ProjectId.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            RepositoryName = name,
            RepositoryPath = path,
            RepositoryUri = TryAbsolute(
                Environment.GetEnvironmentVariable("CI_PROJECT_URL")),
            Branch = string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("CI_COMMIT_TAG"))
                ? Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME")
                : null,
            SourceRevision = NormalizeRevision(
                Environment.GetEnvironmentVariable("CI_COMMIT_SHA")),
            BeforeRevision = NormalizeRevision(
                Environment.GetEnvironmentVariable("CI_COMMIT_BEFORE_SHA")),
            Actor = Environment.GetEnvironmentVariable("GITLAB_USER_LOGIN"),
            RunId = Environment.GetEnvironmentVariable("CI_PIPELINE_ID"),
            RunUri = TryAbsolute(
                Environment.GetEnvironmentVariable("CI_PIPELINE_URL")),
            CompareUri = null,
            IsCi = IsTrue(Environment.GetEnvironmentVariable("GITLAB_CI")),
        });
    }

    /// <inheritdoc/>
    public async Task<ChangeRequest> CreateOrUpdateChangeRequestAsync(
        CreateChangeRequestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = ProviderUri.WithQuery(
            ProviderUri.Append(_projectApi, "merge_requests"),
            ("state", "opened"),
            ("source_branch", request.SourceBranch),
            ("target_branch", request.TargetBranch),
            ("per_page", "100"));
        using var existingDocument = await SendJsonAsync(
                HttpMethod.Get,
                query,
                null,
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        var matches = existingDocument.RootElement
            .EnumerateArray()
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidDataException(
                "GitLab returned duplicate open merge requests.");
        }

        if (matches.Length == 0)
        {
            using var created = await SendJsonAsync(
                    HttpMethod.Post,
                    ProviderUri.Append(_projectApi, "merge_requests"),
                    CreateMergeRequestBody(request),
                    CreatedStatus,
                    cancellationToken)
                .ConfigureAwait(false);
            return MapChangeRequest(
                created.RootElement,
                request,
                OperationDisposition.Created);
        }

        var existing = matches[0];
        request = request with
        {
            Body = IssueClosingDirectiveMerger.Merge(
                request.Body,
                GetOptionalString(existing, "description") ?? "",
                request.PreserveIssueClosingDirectives),
        };

        if (MergeRequestMatches(existing, request))
        {
            return MapChangeRequest(
                existing,
                request,
                OperationDisposition.Reused);
        }

        using var updated = await SendJsonAsync(
                HttpMethod.Put,
                ProviderUri.Append(
                    _projectApi,
                    "merge_requests",
                    GetInt64(existing, "iid").ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                CreateMergeRequestBody(request),
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        return MapChangeRequest(
            updated.RootElement,
            request,
            OperationDisposition.Updated);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PublishedArtifact>> PublishArtifactsAsync(
        ArtifactPublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PublishPackagesAsync(
            request.Version,
            request.Artifacts,
            artifactRoot,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ReleasePublicationResult> PublishReleaseAsync(
        ReleasePublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTagAsync(
                request.TagName,
                request.TargetRevision,
                cancellationToken)
            .ConfigureAwait(false);
        var artifacts = await PublishPackagesAsync(
                request.Version,
                request.Artifacts,
                artifactRoot,
                cancellationToken)
            .ConfigureAwait(false);
        using var existing = await FindReleaseAsync(
                request.TagName,
                cancellationToken)
            .ConfigureAwait(false);
        var disposition = OperationDisposition.Reused;
        JsonDocument release;

        if (existing is null)
        {
            release = await CreateReleaseAsync(
                    request,
                    artifacts,
                    cancellationToken)
                .ConfigureAwait(false);
            disposition = OperationDisposition.Created;
        }
        else if (ReleaseMatches(existing.RootElement, request, artifacts))
        {
            release = JsonDocument.Parse(existing.RootElement.GetRawText());
        }
        else
        {
            release = await UpdateReleaseAsync(
                    request,
                    artifacts,
                    cancellationToken)
                .ConfigureAwait(false);
            disposition = OperationDisposition.Updated;
        }

        using (release)
        {
            return new()
            {
                Release = new()
                {
                    TagName = request.TagName,
                    Title = request.Title,
                    Uri = GetUri(release.RootElement, "_links", "self"),
                    IsDraft = false,
                    IsPrerelease = request.IsPrerelease,
                    Disposition = disposition,
                },
                Artifacts = artifacts,
            };
        }
    }

    /// <inheritdoc/>
    public Task<ReleasePublicationResult> FinalizeReleaseAsync(
        ReleasePublicationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Artifacts.Count != 0)
        {
            throw new InvalidDataException(
                "Release-only finalization forbids artifacts.");
        }

        return PublishReleaseAsync(
            request,
            new DirectoryInfo(Path.GetTempPath()),
            cancellationToken);
    }

    private static object CreateMergeRequestBody(
        CreateChangeRequestRequest request)
    {
        return new
        {
            source_branch = request.SourceBranch,
            target_branch = request.TargetBranch,
            title = request.Title,
            description = request.Body,
            draft = request.Draft,
            labels = string.Join(',', request.Labels),
        };
    }

    private async Task EnsureTagAsync(
        string tagName,
        string targetRevision,
        CancellationToken cancellationToken)
    {
        var uri = ProviderUri.Append(
            _projectApi,
            "repository",
            "tags",
            tagName);
        using var response = await SendAsync(
                HttpMethod.Get,
                uri,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            using var created = await SendJsonAsync(
                    HttpMethod.Post,
                    ProviderUri.Append(
                        _projectApi,
                        "repository",
                        "tags"),
                    new
                    {
                        tag_name = tagName,
                        @ref = targetRevision,
                    },
                    CreatedStatus,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        EnsureStatus(response, OkStatus);
        using var document = await ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var target = GetString(
            GetProperty(document.RootElement, "commit"),
            "id");

        if (target.Equals(targetRevision, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            "The existing GitLab tag points to a different revision.");
    }

    private async Task<IReadOnlyList<PublishedArtifact>> PublishPackagesAsync(
        string version,
        IReadOnlyList<PipelineArtifact> artifacts,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        var results = new List<PublishedArtifact>();

        foreach (var artifact in artifacts.OrderBy(
                     item => item.LogicalName,
                     StringComparer.Ordinal))
        {
            var file = await ResolveArtifactAsync(
                    artifactRoot,
                    artifact,
                    cancellationToken)
                .ConfigureAwait(false);
            var fileName = Path.GetFileName(file.FullName);
            var remote = await FindPackageFileAsync(
                    artifact.LogicalName,
                    version,
                    fileName,
                    cancellationToken)
                .ConfigureAwait(false);
            var downloadUri = ProviderUri.Append(
                _projectApi,
                "packages",
                "generic",
                artifact.LogicalName,
                version,
                fileName);

            if (remote is not null)
            {
                if (remote.Value.Size != artifact.Size
                    || !remote.Value.Sha256.Equals(
                        artifact.Sha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "An existing GitLab Generic Package file does not match.");
                }

                results.Add(new()
                {
                    LogicalName = artifact.LogicalName,
                    FileName = fileName,
                    Uri = downloadUri,
                    Sha256 = artifact.Sha256,
                    Size = artifact.Size,
                    ContentType = "application/octet-stream",
                    Disposition = OperationDisposition.Reused,
                });
                continue;
            }

#pragma warning disable CA2000 // The using statement and request both own a safe idempotent disposal.
            using var content = new FileStreamHttpContent(file);
#pragma warning restore CA2000
            content.Headers.ContentType =
                new("application/octet-stream");
            using var response = await SendAsync(
                    HttpMethod.Put,
                    downloadUri,
                    content,
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureStatus(response, CreatedStatus);
            results.Add(new()
            {
                LogicalName = artifact.LogicalName,
                FileName = fileName,
                Uri = downloadUri,
                Sha256 = artifact.Sha256,
                Size = artifact.Size,
                ContentType = "application/octet-stream",
                Disposition = OperationDisposition.Created,
            });
        }

        return results;
    }

    private async Task<(long Size, string Sha256)?> FindPackageFileAsync(
        string packageName,
        string version,
        string fileName,
        CancellationToken cancellationToken)
    {
        var packagesUri = ProviderUri.WithQuery(
            ProviderUri.Append(_projectApi, "packages"),
            ("package_name", packageName),
            ("package_version", version),
            ("package_type", "generic"),
            ("status", "default"),
            ("per_page", "100"));
        using var packages = await SendJsonAsync(
                HttpMethod.Get,
                packagesUri,
                null,
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        var matches = packages.RootElement
            .EnumerateArray()
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidDataException(
                "GitLab returned duplicate Generic Package identities.");
        }

        if (matches.Length == 0)
        {
            return null;
        }

        var packageId = GetInt64(matches[0], "id");
        using var files = await SendJsonAsync(
                HttpMethod.Get,
                ProviderUri.Append(
                    _projectApi,
                    "packages",
                    packageId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    "package_files"),
                null,
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        var fileMatches = files.RootElement
            .EnumerateArray()
            .Where(file => GetString(file, "file_name").Equals(
                fileName,
                StringComparison.Ordinal))
            .ToArray();

        return fileMatches.Length switch
        {
            0 => null,
            1 => (
                GetInt64(fileMatches[0], "size"),
                GetString(fileMatches[0], "file_sha256")),
            _ => throw new InvalidDataException(
                "GitLab returned duplicate Generic Package files."),
        };
    }

    private async Task<JsonDocument?> FindReleaseAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Get,
                ProviderUri.Append(_projectApi, "releases", tagName),
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureStatus(response, OkStatus);
        return await ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<JsonDocument> CreateReleaseAsync(
        ReleasePublicationRequest request,
        IReadOnlyList<PublishedArtifact> artifacts,
        in CancellationToken cancellationToken)
    {
        return SendJsonAsync(
            HttpMethod.Post,
            ProviderUri.Append(_projectApi, "releases"),
            CreateReleaseBody(request, artifacts),
            CreatedStatus,
            cancellationToken);
    }

    private Task<JsonDocument> UpdateReleaseAsync(
        ReleasePublicationRequest request,
        IReadOnlyList<PublishedArtifact> artifacts,
        in CancellationToken cancellationToken)
    {
        return SendJsonAsync(
            HttpMethod.Put,
            ProviderUri.Append(
                _projectApi,
                "releases",
                request.TagName),
            CreateReleaseBody(request, artifacts),
            OkStatus,
            cancellationToken);
    }

    private static object CreateReleaseBody(
        ReleasePublicationRequest request,
        IReadOnlyList<PublishedArtifact> artifacts)
    {
        return new
        {
            name = request.Title,
            tag_name = request.TagName,
            @ref = request.TargetRevision,
            description = request.Body,
            assets = new
            {
                links = artifacts.Select(artifact => new
                {
                    name = artifact.LogicalName,
                    url = artifact.Uri.AbsoluteUri,
                    link_type = "package",
                }),
            },
        };
    }

    private static bool MergeRequestMatches(
        in JsonElement element,
        CreateChangeRequestRequest request)
    {
        var labels = GetProperty(element, "labels")
            .EnumerateArray()
            .Select(label => label.GetString() ?? "")
            .OrderBy(label => label, StringComparer.Ordinal);
        return GetString(element, "title") == request.Title
               && (GetOptionalString(element, "description") ?? "")
               == request.Body
               && GetBoolean(element, "draft") == request.Draft
               && labels.SequenceEqual(
                   request.Labels,
                   StringComparer.Ordinal);
    }

    private static bool ReleaseMatches(
        in JsonElement element,
        ReleasePublicationRequest request,
        IReadOnlyList<PublishedArtifact> artifacts)
    {
        var links = GetProperty(
                GetProperty(element, "assets"),
                "links")
            .EnumerateArray()
            .Select(link => (
                Name: GetString(link, "name"),
                Url: GetString(link, "url")))
            .OrderBy(link => link.Name, StringComparer.Ordinal)
            .ToArray();
        var expected = artifacts
            .Select(artifact => (
                Name: artifact.LogicalName,
                Url: artifact.Uri.AbsoluteUri))
            .OrderBy(link => link.Name, StringComparer.Ordinal)
            .ToArray();
        return GetString(element, "tag_name") == request.TagName
               && GetString(element, "name") == request.Title
               && (GetOptionalString(element, "description") ?? "")
               == request.Body
               && links.SequenceEqual(expected);
    }

    private static ChangeRequest MapChangeRequest(
        in JsonElement element,
        CreateChangeRequestRequest request,
        in OperationDisposition disposition)
    {
        return new()
        {
            Id = GetInt64(element, "iid").ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            SourceBranch = request.SourceBranch,
            TargetBranch = request.TargetBranch,
            Title = request.Title,
            Uri = new(GetString(element, "web_url"), UriKind.Absolute),
            State = ChangeRequestState.Open,
            IsDraft = request.Draft,
            Labels = request.Labels.ToArray(),
            Disposition = disposition,
        };
    }

    private async Task<JsonDocument> SendJsonAsync(
        HttpMethod method,
        Uri uri,
        object? body,
        HttpStatusCode[] expected,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                method,
                uri,
                body is null ? null : JsonContent.Create(body),
                cancellationToken)
            .ConfigureAwait(false);
        EnsureStatus(response, expected);
        return await ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (!SameOrigin(uri, connection.ApiUrl))
        {
            throw new InvalidDataException(
                "GitLab credentials cannot be sent to another origin.");
        }

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (connection.AuthenticationMode
            == GitLabAuthenticationMode.JobToken)
        {
            request.Headers.Add("JOB-TOKEN", token);
        }
        else if (connection.AuthenticationMode
                 == GitLabAuthenticationMode.PrivateToken)
        {
            request.Headers.Add("PRIVATE-TOKEN", token);
        }
        else
        {
            request.Headers.Authorization =
                new("Bearer", token);
        }

        request.Content = content;

        try
        {
            return await transport.SendAsync(
                    request,
                    connection.RequestTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            request.Dispose();
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken)
            .ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void EnsureStatus(
        HttpResponseMessage response,
        HttpStatusCode[] expected)
    {
        if (expected.Contains(response.StatusCode))
        {
            return;
        }

        throw new InvalidOperationException(
            $"GitLab API request failed with status {(int)response.StatusCode}.");
    }

    private static async Task<FileInfo> ResolveArtifactAsync(
        DirectoryInfo root,
        PipelineArtifact artifact,
        CancellationToken cancellationToken)
    {
        var rootPath = Path.GetFullPath(root.FullName);
        var path = Path.GetFullPath(Path.Combine(
            rootPath,
            artifact.RelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        var prefix = Path.TrimEndingDirectorySeparator(rootPath)
                     + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!path.StartsWith(prefix, comparison)
            || !File.Exists(path))
        {
            throw new InvalidDataException(
                "A GitLab publication artifact path is invalid.");
        }

        var file = new FileInfo(path);

        if (file.Length != artifact.Size)
        {
            throw new InvalidDataException(
                "A GitLab publication artifact size changed.");
        }

        var stream = file.OpenRead();
        string hash;
        await using (stream.ConfigureAwait(false))
        {
            hash = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken)
                    .ConfigureAwait(false));
        }

        if (!hash.Equals(artifact.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A GitLab publication artifact hash changed.");
        }

        return file;
    }

    private static JsonElement GetProperty(
        in JsonElement element,
        string name)
    {
        return element.TryGetProperty(name, out var value)
            ? value
            : throw new InvalidDataException(
                "GitLab returned an incomplete response.");
    }

    private static string GetString(
        in JsonElement element,
        string name)
    {
        return GetProperty(element, name).GetString()
               ?? throw new InvalidDataException(
                   "GitLab returned an invalid string.");
    }

    private static string? GetOptionalString(
        in JsonElement element,
        string name)
    {
        return element.TryGetProperty(name, out var value)
               && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static long GetInt64(
        in JsonElement element,
        string name)
    {
        return GetProperty(element, name).GetInt64();
    }

    private static bool GetBoolean(
        in JsonElement element,
        string name)
    {
        return GetProperty(element, name).GetBoolean();
    }

    private static Uri GetUri(
        in JsonElement element,
        string objectName,
        string propertyName)
    {
        return new(
            GetString(GetProperty(element, objectName), propertyName),
            UriKind.Absolute);
    }

    private static bool SameOrigin(Uri first, Uri second)
    {
        return first.Scheme.Equals(
                   second.Scheme,
                   StringComparison.OrdinalIgnoreCase)
               && first.Host.Equals(
                   second.Host,
                   StringComparison.OrdinalIgnoreCase)
               && first.Port == second.Port;
    }

    private static Uri? TryAbsolute(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static bool IsTrue(string? value)
    {
        return string.Equals(
            value,
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRevision(string? value)
    {
        return value?.All(character => character == '0') == true
            ? null
            : value;
    }
}