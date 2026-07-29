// -----------------------------------------------------------------------
// <copyright file="GitHubRepositoryProvider.cs" company="TedToolkit">
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
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Implements GitHub operations over the internal REST transport.
/// </summary>
/// <param name="connection">The validated non-secret connection.</param>
/// <param name="token">The resolved owning-action token.</param>
/// <param name="transport">The redirect-free transport.</param>
internal sealed class GitHubRepositoryProvider(
    ResolvedGitHubConnection connection,
    string token,
    IProviderHttpTransport transport) : IRepositoryProvider
{
    private static readonly HttpStatusCode[] OkStatus =
        [HttpStatusCode.OK,];

    private static readonly HttpStatusCode[] CreatedStatus =
        [HttpStatusCode.Created,];

    private readonly Uri _repositoryApi = ProviderUri.Append(
        connection.ApiUrl,
        "repos",
        connection.Owner,
        connection.Repository);

    /// <inheritdoc/>
    public Task<RepositoryContext> GetContextAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var branch = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_REF_TYPE"),
            "branch",
            StringComparison.Ordinal)
            ? Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
            : null;
        return Task.FromResult<RepositoryContext>(new()
        {
            RepositoryId =
                $"{connection.Owner}/{connection.Repository}",
            RepositoryName = connection.Repository,
            RepositoryPath =
                $"{connection.Owner}/{connection.Repository}",
            RepositoryUri = ProviderUri.Append(
                connection.InstanceUrl,
                connection.Owner,
                connection.Repository),
            Branch = branch,
            SourceRevision = NormalizeRevision(
                Environment.GetEnvironmentVariable("GITHUB_SHA")),
            BeforeRevision = NormalizeRevision(
                GitHubActionsEnvironment.ReadBeforeRevision()),
            Actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR"),
            RunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
            RunUri = CreateRunUri(),
            CompareUri = null,
            IsCi = IsTrue(Environment.GetEnvironmentVariable(
                "GITHUB_ACTIONS")),
        });
    }

    /// <inheritdoc/>
    public async Task<ChangeRequest> CreateOrUpdateChangeRequestAsync(
        CreateChangeRequestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = ProviderUri.WithQuery(
            ProviderUri.Append(_repositoryApi, "pulls"),
            ("state", "open"),
            ("head", $"{connection.Owner}:{request.SourceBranch}"),
            ("base", request.TargetBranch),
            ("per_page", "100"));
        using var existingDocument = await SendJsonAsync(
                HttpMethod.Get,
                query,
                (object?)null,
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        var matches = existingDocument.RootElement
            .EnumerateArray()
            .ToArray();

        if (matches.Length > 1)
        {
            throw new InvalidDataException(
                "GitHub returned duplicate open change requests.");
        }

        if (matches.Length == 0)
        {
            using var created = await SendJsonAsync(
                    HttpMethod.Post,
                    ProviderUri.Append(_repositoryApi, "pulls"),
                    new
                    {
                        title = request.Title,
                        head = request.SourceBranch,
                        @base = request.TargetBranch,
                        body = request.Body,
                        draft = request.Draft,
                    },
                    CreatedStatus,
                    cancellationToken)
                .ConfigureAwait(false);
            var number = GetInt64(created.RootElement, "number");
            await SetLabelsAsync(number, request.Labels, cancellationToken)
                .ConfigureAwait(false);
            return MapChangeRequest(
                created.RootElement,
                request,
                OperationDisposition.Created);
        }

        var existing = matches[0];
        var numberValue = GetInt64(existing, "number");
        request = request with
        {
            Body = IssueClosingDirectiveMerger.Merge(
                request.Body,
                GetOptionalString(existing, "body") ?? "",
                request.PreserveIssueClosingDirectives),
        };
        var equal = GetString(existing, "title") == request.Title
                    && (GetOptionalString(existing, "body") ?? "") == request.Body
                    && GetBoolean(existing, "draft") == request.Draft
                    && ReadLabels(existing).SequenceEqual(
                        request.Labels,
                        StringComparer.Ordinal);

        if (equal)
        {
            return MapChangeRequest(
                existing,
                request,
                OperationDisposition.Reused);
        }

        using var updated = await SendJsonAsync(
                HttpMethod.Patch,
                ProviderUri.Append(
                    _repositoryApi,
                    "pulls",
                    numberValue.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                new
                {
                    title = request.Title,
                    body = request.Body,
                    @base = request.TargetBranch,
                },
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        await SetLabelsAsync(numberValue, request.Labels, cancellationToken)
            .ConfigureAwait(false);
        return MapChangeRequest(
            updated.RootElement,
            request,
            OperationDisposition.Updated);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PublishedArtifact>> PublishArtifactsAsync(
        ArtifactPublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifactRoot);

        if (string.IsNullOrWhiteSpace(request.TagName))
        {
            throw new InvalidDataException(
                "GitHub artifact publication requires an existing Release tag.");
        }

        using var release = await FindReleaseAsync(
                request.TagName,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "The matching GitHub Release does not exist.");
        return await PublishAssetsAsync(
                release.RootElement,
                request.Artifacts,
                artifactRoot,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReleasePublicationResult> PublishReleaseAsync(
        ReleasePublicationRequest request,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(artifactRoot);
        await EnsureTagAsync(
                request.TagName,
                request.TargetRevision,
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
            release = await CreateReleaseAsync(request, cancellationToken)
                .ConfigureAwait(false);
            disposition = OperationDisposition.Created;
        }
        else
        {
            release = JsonDocument.Parse(existing.RootElement.GetRawText());

            if (!ReleaseMatches(release.RootElement, request))
            {
                release.Dispose();
                release = await UpdateReleaseAsync(
                        GetInt64(existing.RootElement, "id"),
                        request,
                        keepDraft: GetBoolean(existing.RootElement, "draft"),
                        cancellationToken)
                    .ConfigureAwait(false);
                disposition = OperationDisposition.Updated;
            }
        }

        using (release)
        {
            var artifacts = await PublishAssetsAsync(
                    release.RootElement,
                    request.Artifacts,
                    artifactRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            var finalRelease = release;

            if (GetBoolean(release.RootElement, "draft"))
            {
                finalRelease = await UpdateReleaseAsync(
                        GetInt64(release.RootElement, "id"),
                        request,
                        keepDraft: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            using (finalRelease == release ? null : finalRelease)
            {
                return new()
                {
                    Release = MapRelease(
                        finalRelease.RootElement,
                        request,
                        disposition),
                    Artifacts = artifacts,
                };
            }
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

    private async Task EnsureTagAsync(
        string tagName,
        string targetRevision,
        CancellationToken cancellationToken)
    {
        var referenceUri = ProviderUri.Append(
            _repositoryApi,
            "git",
            "ref",
            "tags",
            tagName);
        using var response = await SendAsync(
                HttpMethod.Get,
                referenceUri,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            using var created = await SendJsonAsync(
                    HttpMethod.Post,
                    ProviderUri.Append(_repositoryApi, "git", "refs"),
                    new
                    {
                        @ref = $"refs/tags/{tagName}",
                        sha = targetRevision,
                    },
                    CreatedStatus,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        EnsureStatus(response, OkStatus);
        using var document = await ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var target = await ResolveTagTargetAsync(
                GetProperty(document.RootElement, "object"),
                cancellationToken)
            .ConfigureAwait(false);

        if (target.Equals(targetRevision, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            "The existing GitHub tag points to a different revision.");
    }

    private async Task<string> ResolveTagTargetAsync(
        JsonElement target,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var depth = 0; depth < 16; depth++)
        {
            var sha = GetString(target, "sha");
            var type = GetOptionalString(target, "type") ?? "commit";

            if (type.Equals("commit", StringComparison.Ordinal))
            {
                return sha;
            }

            if (!type.Equals("tag", StringComparison.Ordinal)
                || !visited.Add(sha))
            {
                break;
            }

            using var response = await SendAsync(
                    HttpMethod.Get,
                    ProviderUri.Append(_repositoryApi, "git", "tags", sha),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureStatus(response, OkStatus);
            using var document = await ReadJsonAsync(
                    response,
                    cancellationToken)
                .ConfigureAwait(false);
            target = GetProperty(document.RootElement, "object").Clone();
        }

        throw new InvalidDataException(
            "The existing GitHub tag target is invalid.");
    }

    private async Task<JsonDocument?> FindReleaseAsync(
        string tagName,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Get,
                ProviderUri.Append(
                    _repositoryApi,
                    "releases",
                    "tags",
                    tagName),
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
        in CancellationToken cancellationToken)
    {
        return SendJsonAsync(
            HttpMethod.Post,
            ProviderUri.Append(_repositoryApi, "releases"),
            new
            {
                tag_name = request.TagName,
                target_commitish = request.TargetRevision,
                name = request.Title,
                body = request.Body,
                draft = true,
                prerelease = request.IsPrerelease,
            },
            CreatedStatus,
            cancellationToken);
    }

    private Task<JsonDocument> UpdateReleaseAsync(
        long releaseId,
        ReleasePublicationRequest request,
        bool keepDraft,
        in CancellationToken cancellationToken)
    {
        return SendJsonAsync(
            HttpMethod.Patch,
            ProviderUri.Append(
                _repositoryApi,
                "releases",
                releaseId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            new
            {
                tag_name = request.TagName,
                target_commitish = request.TargetRevision,
                name = request.Title,
                body = request.Body,
                draft = keepDraft,
                prerelease = request.IsPrerelease,
            },
            OkStatus,
            cancellationToken);
    }

    private async Task<IReadOnlyList<PublishedArtifact>> PublishAssetsAsync(
        JsonElement release,
        IReadOnlyList<PipelineArtifact> artifacts,
        DirectoryInfo artifactRoot,
        CancellationToken cancellationToken)
    {
        if (artifacts.Count == 0)
        {
            return [];
        }

        var releaseId = GetInt64(release, "id");
        using var existing = await SendJsonAsync(
                HttpMethod.Get,
                ProviderUri.Append(
                    _repositoryApi,
                    "releases",
                    releaseId.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    "assets"),
                (object?)null,
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
        var existingAssets = existing.RootElement
            .EnumerateArray()
            .ToArray();
        var uploadTemplate = GetString(release, "upload_url");
        var templateIndex = uploadTemplate.IndexOf(
            '{',
            StringComparison.Ordinal);
        var uploadBase = new Uri(
            templateIndex < 0
                ? uploadTemplate
                : uploadTemplate[..templateIndex],
            UriKind.Absolute);
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
            var matches = existingAssets.Where(candidate =>
                    GetString(candidate, "name").Equals(
                        fileName,
                        StringComparison.Ordinal))
                .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidDataException(
                    "GitHub returned duplicate Release assets.");
            }

            if (matches.Length == 1)
            {
                results.Add(MapExistingAsset(matches[0], artifact, fileName));
                continue;
            }

#pragma warning disable CA2000 // The using statement and request both own a safe idempotent disposal.
            using (var content = new FileStreamHttpContent(file))
#pragma warning restore CA2000
            {
                content.Headers.ContentType =
                    new("application/octet-stream");
                var uploadUri = ProviderUri.WithQuery(
                    uploadBase,
                    ("name", fileName),
                    ("label", artifact.LogicalName));
                using var uploaded = await SendJsonAsync(
                        HttpMethod.Post,
                        uploadUri,
                        content,
                        CreatedStatus,
                        cancellationToken,
                        allowUploadOrigin: true)
                    .ConfigureAwait(false);
                results.Add(MapUploadedAsset(
                    uploaded.RootElement,
                    artifact,
                    fileName));
            }
        }

        return results;
    }

    private static PublishedArtifact MapExistingAsset(
        in JsonElement asset,
        PipelineArtifact artifact,
        string fileName)
    {
        var digest = GetOptionalString(asset, "digest");

        if (GetInt64(asset, "size") != artifact.Size
            || !string.Equals(
                digest,
                $"sha256:{artifact.Sha256}",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "An existing GitHub Release asset does not match.");
        }

        return new()
        {
            LogicalName = artifact.LogicalName,
            FileName = fileName,
            Uri = GetUri(asset, "browser_download_url"),
            Sha256 = artifact.Sha256,
            Size = artifact.Size,
            ContentType = GetOptionalString(asset, "content_type"),
            Disposition = OperationDisposition.Reused,
        };
    }

    private static PublishedArtifact MapUploadedAsset(
        in JsonElement asset,
        PipelineArtifact artifact,
        string fileName)
    {
        return new()
        {
            LogicalName = artifact.LogicalName,
            FileName = fileName,
            Uri = GetUri(asset, "browser_download_url"),
            Sha256 = artifact.Sha256,
            Size = artifact.Size,
            ContentType = GetOptionalString(asset, "content_type"),
            Disposition = OperationDisposition.Created,
        };
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
                "A GitHub publication artifact path is invalid.");
        }

        var file = new FileInfo(path);

        if (file.Length != artifact.Size)
        {
            throw new InvalidDataException(
                "A GitHub publication artifact size changed.");
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
                "A GitHub publication artifact hash changed.");
        }

        return file;
    }

    private async Task SetLabelsAsync(
        long number,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken)
    {
        using var document = await SendJsonAsync(
                HttpMethod.Post,
                ProviderUri.Append(
                    _repositoryApi,
                    "issues",
                    number.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    "labels"),
                new
                {
                    labels,
                },
                OkStatus,
                cancellationToken)
            .ConfigureAwait(false);
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

    private async Task<JsonDocument> SendJsonAsync(
        HttpMethod method,
        Uri uri,
        HttpContent content,
        HttpStatusCode[] expected,
        CancellationToken cancellationToken,
        bool allowUploadOrigin = false)
    {
        using var response = await SendAsync(
                method,
                uri,
                content,
                cancellationToken,
                allowUploadOrigin)
            .ConfigureAwait(false);
        EnsureStatus(response, expected);
        return await ReadJsonAsync(response, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        HttpContent? content,
        CancellationToken cancellationToken,
        bool allowUploadOrigin = false)
    {
        if (!SameOrigin(uri, connection.ApiUrl)
            && (!allowUploadOrigin || !IsTrustedUploadOrigin(uri)))
        {
            throw new InvalidDataException(
                "GitHub credentials cannot be sent to another origin.");
        }

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization =
            new("Bearer", token);
        request.Headers.Accept.Add(
            new(
                "application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("TedToolkit.Combine/1");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
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

    private bool IsTrustedUploadOrigin(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps
               && string.IsNullOrEmpty(uri.UserInfo)
               && string.IsNullOrEmpty(uri.Fragment)
               && connection.ApiUrl.Scheme == Uri.UriSchemeHttps
               && connection.ApiUrl.Host.Equals(
                   "api.github.com",
                   StringComparison.OrdinalIgnoreCase)
               && uri.Host.Equals(
                   "uploads.github.com",
                   StringComparison.OrdinalIgnoreCase)
               && uri.IsDefaultPort;
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
            $"GitHub API request failed with status {(int)response.StatusCode}.");
    }

    private static ChangeRequest MapChangeRequest(
        in JsonElement element,
        CreateChangeRequestRequest request,
        in OperationDisposition disposition)
    {
        return new()
        {
            Id = GetInt64(element, "number").ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            SourceBranch = request.SourceBranch,
            TargetBranch = request.TargetBranch,
            Title = request.Title,
            Uri = GetUri(element, "html_url"),
            State = ChangeRequestState.Open,
            IsDraft = request.Draft,
            Labels = request.Labels.ToArray(),
            Disposition = disposition,
        };
    }

    private static Release MapRelease(
        in JsonElement element,
        ReleasePublicationRequest request,
        in OperationDisposition disposition)
    {
        return new()
        {
            TagName = request.TagName,
            Title = request.Title,
            Uri = GetUri(element, "html_url"),
            IsDraft = GetBoolean(element, "draft"),
            IsPrerelease = request.IsPrerelease,
            Disposition = disposition,
        };
    }

    private static bool ReleaseMatches(
        in JsonElement element,
        ReleasePublicationRequest request)
    {
        return GetString(element, "tag_name") == request.TagName
               && GetString(element, "name") == request.Title
               && (GetOptionalString(element, "body") ?? "") == request.Body
               && GetBoolean(element, "prerelease")
               == request.IsPrerelease;
    }

    private static string[] ReadLabels(in JsonElement element)
    {
        return GetProperty(element, "labels")
            .EnumerateArray()
            .Select(label => GetString(label, "name"))
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
    }

    private Uri? CreateRunUri()
    {
        var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
        return string.IsNullOrWhiteSpace(runId)
            ? null
            : ProviderUri.Append(
                connection.InstanceUrl,
                connection.Owner,
                connection.Repository,
                "actions",
                "runs",
                runId);
    }

    private static JsonElement GetProperty(
        in JsonElement element,
        string name)
    {
        return element.TryGetProperty(name, out var value)
            ? value
            : throw new InvalidDataException(
                "GitHub returned an incomplete response.");
    }

    private static string GetString(in JsonElement element, string name)
    {
        return GetProperty(element, name).GetString()
               ?? throw new InvalidDataException(
                   "GitHub returned an invalid string.");
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

    private static long GetInt64(in JsonElement element, string name)
    {
        return GetProperty(element, name).GetInt64();
    }

    private static bool GetBoolean(in JsonElement element, string name)
    {
        return GetProperty(element, name).GetBoolean();
    }

    private static Uri GetUri(in JsonElement element, string name)
    {
        return new(GetString(element, name), UriKind.Absolute);
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