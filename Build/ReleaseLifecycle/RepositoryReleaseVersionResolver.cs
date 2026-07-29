using System.Text.Json;
using System.Text.Json.Serialization;

using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.Build.ReleaseLifecycle;

/// <summary>
/// Resolves the next coordinated version from accepted history and local tags.
/// </summary>
internal sealed class RepositoryReleaseVersionResolver(
    DirectoryInfo repositoryRoot)
{
    private static readonly string[] ExpectedPackageIds =
    [
        "TedToolkit.CodeAnalysis",
        "TedToolkit.ModularPipelines.Build",
        "TedToolkit.ModularPipelines.Combine",
    ];

    private const string SuccessHeader = "tedtoolkit-nuget-success-v1";
    private const string PendingPrefix = "nuget-pending/";
    private const string PackagePrefix = "nuget-package/";
    private const string FinalizedPrefix = "nuget-finalized/";
    private const string AbandonedPrefix = "nuget-abandoned/";
    private readonly GitTagStore _tags = new(repositoryRoot);

    /// <summary>
    /// Resolves the next version using a caller-selected local date.
    /// </summary>
    /// <param name="date">The runner-local release date.</param>
    /// <param name="sourceRevision">The exact source revision.</param>
    /// <param name="baselineFile">The immutable schema-1 baseline.</param>
    /// <param name="cancellationToken">A token that cancels Git reads.</param>
    /// <returns>The normalized calendar version.</returns>
    public async Task<string> ResolveAsync(
        DateOnly date,
        string sourceRevision,
        FileInfo baselineFile,
        CancellationToken cancellationToken)
    {
        var baseline = await ReadBaselineAsync(
                baselineFile,
                cancellationToken)
            .ConfigureAwait(false);
        var tags = await _tags.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false);
        var byName = tags.ToDictionary(
            tag => tag.Name,
            StringComparer.Ordinal);
        var consumed = new List<ConsumedReleaseVersionRecord>();

        foreach (var release in baseline.Releases)
        {
            if (!byName.TryGetValue(release.TagName, out var tag)
                || !tag.TargetRevision.Equals(
                    release.TargetRevision,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The accepted release-history baseline conflicts with local tags.");
            }

            consumed.Add(new()
            {
                Version = release.Version,
                SourceRevision = release.TargetRevision,
                Disposition = ConsumedReleaseDisposition.PackageSucceeded,
            });
        }

        var baselineTags = baseline.Releases
            .Select(release => release.TagName)
            .ToHashSet(StringComparer.Ordinal);
        var states = new Dictionary<string, LifecycleState>(
            StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            if (baselineTags.Contains(tag.Name))
            {
                continue;
            }

            if (tag.Name.StartsWith(PendingPrefix, StringComparison.Ordinal))
            {
                var version = tag.Name[PendingPrefix.Length..];
                RequireNormalizedVersion(version);
                GetState(states, version).Pending = new(
                    tag,
                    ParseExact(
                        tag,
                        "tedtoolkit-nuget-pending-v1",
                        "manifest-sha256",
                        "run-identity"));
                continue;
            }

            if (tag.Name.StartsWith(AbandonedPrefix, StringComparison.Ordinal))
            {
                var version = tag.Name[AbandonedPrefix.Length..];
                RequireNormalizedVersion(version);
                GetState(states, version).Abandoned = new(
                    tag,
                    ParseExact(
                        tag,
                        "tedtoolkit-nuget-abandoned-v1",
                        "manifest-sha256",
                        "run-identity",
                        "audit-reference"));
                continue;
            }

            if (tag.Name.StartsWith(FinalizedPrefix, StringComparison.Ordinal))
            {
                var version = tag.Name[FinalizedPrefix.Length..];
                RequireNormalizedVersion(version);
                GetState(states, version).Finalized = new(
                    tag,
                    ParseExact(
                        tag,
                        "tedtoolkit-nuget-finalized-v1",
                        "manifest-sha256",
                        "run-identity",
                        "recovery-run-identity",
                        "audit-reference"));
                continue;
            }

            if (tag.Name.StartsWith(PackagePrefix, StringComparison.Ordinal))
            {
                AddProgress(states, tag);
                continue;
            }

            if (LooksLikeCalendarVersion(tag.Name))
            {
                RequireNormalizedVersion(tag.Name);
                GetState(states, tag.Name).Success = new(
                    tag,
                    ParseExact(
                        tag,
                        SuccessHeader,
                        "manifest-sha256",
                        "run-identity"));
            }
        }

        foreach (var (version, state) in states)
        {
            ValidateState(version, state);

            if (state.Success is not null)
            {
                consumed.Add(new()
                {
                    Version = version,
                    SourceRevision = state.Success.Tag.TargetRevision,
                    Disposition =
                        ConsumedReleaseDisposition.PackageSucceeded,
                });
            }
            else if (state.Abandoned is not null)
            {
                consumed.Add(new()
                {
                    Version = version,
                    SourceRevision = state.Abandoned.Tag.TargetRevision,
                    Disposition = ConsumedReleaseDisposition.Abandoned,
                });
            }
        }

        var resolution = new DailyReleaseVersionPolicy().Resolve(new()
        {
            Date = date,
            SourceRevision = sourceRevision,
            ConsumedVersions = consumed,
            HasActivePublicationReservation = states.Values.Any(
                state => state.Pending is not null),
        });
        return resolution.Kind switch
        {
            ReleaseVersionResolutionKind.NewVersion => resolution.Version!,
            ReleaseVersionResolutionKind.PublicationRecoveryRequired
                => throw new InvalidOperationException(
                    "An active publication reservation requires recovery."),
            ReleaseVersionResolutionKind.Exhausted
                => throw new InvalidOperationException(
                    "The daily release counter is exhausted."),
            _ => throw new InvalidOperationException(
                "The release version resolution is invalid."),
        };
    }

    private static async Task<ReleaseHistoryFile> ReadBaselineAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        if (!file.Exists || file.LinkTarget is not null)
        {
            throw new InvalidDataException(
                "The accepted release-history baseline is unavailable.");
        }

        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var baseline = await JsonSerializer.DeserializeAsync<ReleaseHistoryFile>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                },
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "The accepted release-history baseline is empty.");

        if (baseline.SchemaVersion != 1
            || baseline.Releases is null
            || baseline.Releases.Any(release =>
                release is null
                || string.IsNullOrWhiteSpace(release.Version)
                || string.IsNullOrWhiteSpace(release.TagName)
                || string.IsNullOrWhiteSpace(release.TargetRevision))
            || baseline.Releases.Select(release => release.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != baseline.Releases.Count
            || baseline.Releases.Select(release => release.TagName)
                .Distinct(StringComparer.Ordinal).Count()
                != baseline.Releases.Count)
        {
            throw new InvalidDataException(
                "The accepted release-history baseline is invalid.");
        }

        return baseline;
    }

    private static void AddProgress(
        IDictionary<string, LifecycleState> states,
        GitTagSnapshot tag)
    {
        var suffix = tag.Name[PackagePrefix.Length..];
        var separator = suffix.LastIndexOf('/');

        if (separator <= 0 || separator == suffix.Length - 1)
        {
            throw new InvalidDataException(
                $"Package-progress tag '{tag.Name}' has an invalid name.");
        }

        var version = suffix[..separator];
        var packageSegment = suffix[(separator + 1)..];
        RequireNormalizedVersion(version);
        var fields = ParseExact(
            tag,
            "tedtoolkit-nuget-package-v1",
            "package-id",
            "package-sha256",
            "symbol-sha256",
            "manifest-sha256",
            "run-identity");
        var packageId = fields["package-id"];

        if (!ExpectedPackageIds.Contains(packageId, StringComparer.Ordinal)
            || !packageSegment.Equals(
                packageId.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Package-progress tag '{tag.Name}' has an invalid package identity.");
        }

        var state = GetState(states, version);

        if (!state.Progress.TryAdd(packageId, new(tag, fields)))
        {
            throw new InvalidDataException(
                $"Package-progress tag '{tag.Name}' is duplicated.");
        }
    }

    private static void ValidateState(string version, LifecycleState state)
    {
        if (state.Success is not null && state.Abandoned is not null
            || state.Finalized is not null && state.Success is null
            || state.Finalized is not null && state.Abandoned is not null
            || state.Progress.Count > 0
            && state.Pending is null
            && state.Success is null
            && state.Abandoned is null)
        {
            throw new InvalidDataException(
                $"Release lifecycle for '{version}' conflicts.");
        }

        ValidateFields(state.Pending);
        ValidateFields(state.Success);
        ValidateFields(state.Abandoned);
        ValidateFields(state.Finalized);
        var anchor = state.Pending
                     ?? state.Success
                     ?? state.Abandoned;

        foreach (var progress in state.Progress.Values)
        {
            ValidateFields(progress);
            RequireMatching(anchor!, progress);
            RequireHash(progress.Fields["package-sha256"]);
            var symbolHash = progress.Fields["symbol-sha256"];

            if (!symbolHash.Equals("none", StringComparison.Ordinal))
            {
                RequireHash(symbolHash);
            }
        }

        if (state.Success is not null
            && (state.Progress.Count != ExpectedPackageIds.Length
                || ExpectedPackageIds.Any(
                    packageId => !state.Progress.ContainsKey(packageId))))
        {
            throw new InvalidDataException(
                $"Package success for '{version}' lacks complete progress.");
        }

        if (state.Pending is not null && state.Success is not null)
        {
            RequireMatching(state.Pending, state.Success);
        }

        if (state.Pending is not null && state.Abandoned is not null)
        {
            RequireMatching(state.Pending, state.Abandoned);
        }

        if (state.Finalized is not null)
        {
            RequireMatching(state.Success!, state.Finalized);
            RequireOpaque(state.Finalized.Fields["audit-reference"]);
            RequireRunIdentity(
                state.Finalized.Fields["recovery-run-identity"]);
        }

        if (state.Abandoned is not null)
        {
            RequireOpaque(state.Abandoned.Fields["audit-reference"]);
        }
    }

    private static void ValidateFields(TagFields? value)
    {
        if (value is null)
        {
            return;
        }

        RequireHash(value.Fields["manifest-sha256"]);
        RequireRunIdentity(value.Fields["run-identity"]);
    }

    private static void RequireMatching(TagFields anchor, TagFields value)
    {
        if (anchor.Tag.TargetRevision.Equals(
                value.Tag.TargetRevision,
                StringComparison.OrdinalIgnoreCase)
            && anchor.Fields["manifest-sha256"].Equals(
                value.Fields["manifest-sha256"],
                StringComparison.Ordinal)
            && anchor.Fields["run-identity"].Equals(
                value.Fields["run-identity"],
                StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            "Lifecycle tag target or copied fields conflict.");
    }

    private static Dictionary<string, string> ParseExact(
        GitTagSnapshot tag,
        string header,
        params string[] expectedFields)
    {
        if (!tag.ObjectType.Equals("tag", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Lifecycle tag '{tag.Name}' must be annotated.");
        }

        var lines = tag.Annotation.Split('\n');

        if (lines.Length != expectedFields.Length + 1
            || !lines[0].Equals(header, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Lifecycle tag '{tag.Name}' has an invalid schema.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < expectedFields.Length; index++)
        {
            var line = lines[index + 1];
            var separator = line.IndexOf('=');
            var expected = expectedFields[index];

            if (separator <= 0
                || !line[..separator].Equals(
                    expected,
                    StringComparison.Ordinal)
                || string.IsNullOrEmpty(line[(separator + 1)..])
                || !result.TryAdd(expected, line[(separator + 1)..]))
            {
                throw new InvalidDataException(
                    $"Lifecycle tag '{tag.Name}' has an invalid field.");
            }
        }

        return result;
    }

    private static LifecycleState GetState(
        IDictionary<string, LifecycleState> states,
        string version)
    {
        if (!states.TryGetValue(version, out var state))
        {
            state = new();
            states.Add(version, state);
        }

        return state;
    }

    private static void RequireHash(string value)
    {
        if (value.Length != 64 || !value.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException(
                "A lifecycle SHA-256 field is invalid.");
        }
    }

    private static void RequireRunIdentity(string value)
    {
        const string Prefix = "github:";

        if (!value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Length == Prefix.Length
            || !value[Prefix.Length..].All(char.IsAsciiDigit))
        {
            throw new InvalidDataException(
                "A lifecycle run identity is invalid.");
        }
    }

    private static void RequireOpaque(string value)
    {
        if (value.Length is < 1 or > 128
            || !char.IsAsciiLetterOrDigit(value[0])
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not ('.' or '_' or '-')))
        {
            throw new InvalidDataException(
                "A lifecycle audit reference is invalid.");
        }
    }

    private static void RequireNormalizedVersion(string value)
    {
        var parts = value.Split('.');

        if (parts.Length is not (3 or 4)
            || parts.Any(part =>
                part.Length == 0
                || part.Length > 1 && part[0] == '0'
                || !int.TryParse(
                    part,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            || !int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month)
            || !int.TryParse(parts[2], out var day)
            || year is < 1 or > 9999
            || !DateOnly.TryParseExact(
                $"{year:D4}-{month:D2}-{day:D2}",
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _)
            || parts.Length == 4
            && (!int.TryParse(parts[3], out var counter)
                || counter is < 1 or > 65534))
        {
            throw new InvalidDataException(
                $"Release version '{value}' is not normalized.");
        }
    }

    private static bool LooksLikeCalendarVersion(string value)
    {
        return value.Length > 0
               && char.IsAsciiDigit(value[0])
               && value.Count(character => character == '.') >= 2;
    }

    private sealed record ReleaseHistoryFile
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("releases")]
        public IReadOnlyList<ReleaseHistoryEntry> Releases { get; init; } = [];
    }

    private sealed record ReleaseHistoryEntry
    {
        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("tagName")]
        public required string TagName { get; init; }

        [JsonPropertyName("targetRevision")]
        public required string TargetRevision { get; init; }
    }

    private sealed record TagFields(
        GitTagSnapshot Tag,
        IReadOnlyDictionary<string, string> Fields);

    private sealed class LifecycleState
    {
        public TagFields? Pending { get; set; }

        public TagFields? Success { get; set; }

        public TagFields? Abandoned { get; set; }

        public TagFields? Finalized { get; set; }

        public Dictionary<string, TagFields> Progress { get; } =
            new(StringComparer.Ordinal);
    }
}
