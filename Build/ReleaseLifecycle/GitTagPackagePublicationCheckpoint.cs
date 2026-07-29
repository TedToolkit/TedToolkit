using System.Globalization;

using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.Build.ReleaseLifecycle;

/// <summary>
/// Records deterministic package publication through immutable annotated tags.
/// </summary>
internal sealed class GitTagPackagePublicationCheckpoint(
    DirectoryInfo repositoryRoot,
    string runIdentity) : IPackagePublicationCheckpoint
{
    private static readonly string[] ExpectedPackageIds =
    [
        "TedToolkit.CodeAnalysis",
        "TedToolkit.ModularPipelines.Build",
        "TedToolkit.ModularPipelines.Combine",
    ];

    private const string PendingHeader = "tedtoolkit-nuget-pending-v1";
    private const string PackageHeader = "tedtoolkit-nuget-package-v1";
    private const string SuccessHeader = "tedtoolkit-nuget-success-v1";
    private readonly GitTagStore _tags = new(repositoryRoot);
    private PackagePublicationCheckpointContext? _context;
    private string? _pendingAnnotation;
    private bool _terminalWithoutPending;

    /// <summary>
    /// Validates terminal lifecycle state before composition and performs only
    /// interrupted terminal pending cleanup.
    /// </summary>
    /// <param name="version">The exact candidate version.</param>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    /// <returns>
    /// <see langword="true"/> when the host must perform no pipeline action.
    /// </returns>
    public async Task<bool> PrepareAsync(
        string version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException(
                "A checkpoint version is required.");
        }

        var pending = await _tags.ReadAsync(
                PendingName(version),
                cancellationToken)
            .ConfigureAwait(false);
        var success = await _tags.ReadAsync(version, cancellationToken)
            .ConfigureAwait(false);
        var abandoned = await _tags.ReadAsync(
                $"nuget-abandoned/{version}",
                cancellationToken)
            .ConfigureAwait(false);
        var finalized = await _tags.ReadAsync(
                $"nuget-finalized/{version}",
                cancellationToken)
            .ConfigureAwait(false);

        if (success is not null && abandoned is not null
            || finalized is not null && success is null
            || finalized is not null && abandoned is not null)
        {
            throw new InvalidDataException(
                "Release lifecycle markers conflict.");
        }

        if (pending is null)
        {
            if (success is not null)
            {
                var fields = ParseExact(
                    success,
                    SuccessHeader,
                    "manifest-sha256",
                    "run-identity");
                ValidateCommonFields(fields);
                await ValidateExpectedProgressAsync(
                        version,
                        success,
                        fields,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (abandoned is not null)
            {
                var fields = ParseExact(
                    abandoned,
                    "tedtoolkit-nuget-abandoned-v1",
                    "manifest-sha256",
                    "run-identity",
                    "audit-reference");
                ValidateCommonFields(fields);
                ValidateOpaque(fields["audit-reference"]);
                await ValidatePresentProgressAsync(
                        version,
                        abandoned,
                        fields,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (finalized is not null)
            {
                var successFields = ParseExact(
                    success!,
                    SuccessHeader,
                    "manifest-sha256",
                    "run-identity");
                var finalizedFields = ParseExact(
                    finalized,
                    "tedtoolkit-nuget-finalized-v1",
                    "manifest-sha256",
                    "run-identity",
                    "recovery-run-identity",
                    "audit-reference");
                ValidateCommonFields(successFields);
                ValidateCommonFields(finalizedFields);
                ValidateRunIdentity(
                    finalizedFields["recovery-run-identity"]);
                ValidateOpaque(finalizedFields["audit-reference"]);
                RequireMatching(
                    success!,
                    finalized,
                    successFields,
                    finalizedFields);
            }

            return success is not null
                   || abandoned is not null
                   || finalized is not null;
        }

        var pendingFields = ParseExact(
            pending,
            PendingHeader,
            "manifest-sha256",
            "run-identity");
        ValidateCommonFields(pendingFields);

        if (abandoned is not null)
        {
            var abandonedFields = ParseExact(
                abandoned,
                "tedtoolkit-nuget-abandoned-v1",
                "manifest-sha256",
                "run-identity",
                "audit-reference");
            ValidateCommonFields(abandonedFields);
            ValidateOpaque(abandonedFields["audit-reference"]);
            RequireMatching(
                pending,
                abandoned,
                pendingFields,
                abandonedFields);
            await ValidatePresentProgressAsync(
                    version,
                    abandoned,
                    abandonedFields,
                    cancellationToken)
                .ConfigureAwait(false);
            await _tags.DeleteMatchingAsync(
                    PendingName(version),
                    pending.TargetRevision,
                    pending.Annotation,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (finalized is not null)
        {
            var successFields = ParseExact(
                success!,
                SuccessHeader,
                "manifest-sha256",
                "run-identity");
            var finalizedFields = ParseExact(
                finalized,
                "tedtoolkit-nuget-finalized-v1",
                "manifest-sha256",
                "run-identity",
                "recovery-run-identity",
                "audit-reference");
            ValidateCommonFields(successFields);
            ValidateCommonFields(finalizedFields);
            ValidateRunIdentity(
                finalizedFields["recovery-run-identity"]);
            ValidateOpaque(finalizedFields["audit-reference"]);
            RequireMatching(
                pending,
                success!,
                pendingFields,
                successFields);
            RequireMatching(
                success!,
                finalized,
                successFields,
                finalizedFields);
            await ValidateExpectedProgressAsync(
                    version,
                    success!,
                    successFields,
                    cancellationToken)
                .ConfigureAwait(false);
            await _tags.DeleteMatchingAsync(
                    PendingName(version),
                    pending.TargetRevision,
                    pending.Annotation,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (success is not null)
        {
            var successFields = ParseExact(
                success,
                SuccessHeader,
                "manifest-sha256",
                "run-identity");
            ValidateCommonFields(successFields);
            RequireMatching(
                pending,
                success,
                pendingFields,
                successFields);
            await ValidateExpectedProgressAsync(
                    version,
                    success,
                    successFields,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ReadCompletedPackageIdsAsync(
        PackagePublicationCheckpointContext context,
        CancellationToken cancellationToken)
    {
        ValidateContext(context);
        _context = context;
        _pendingAnnotation = CreatePendingAnnotation(context);
        var existingPending = await _tags.ReadAsync(
                PendingName(context.Version),
                cancellationToken)
            .ConfigureAwait(false);
        var existingSuccess = await _tags.ReadAsync(
                context.Version,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingPending is null && existingSuccess is not null)
        {
            ValidateTag(
                existingSuccess,
                context.SourceRevision,
                CreateSuccessAnnotation(context));
            var terminalCompleted = new List<string>();

            foreach (var package in context.Packages)
            {
                var progress = await _tags.ReadAsync(
                        PackageName(context.Version, package.PackageId),
                        cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new InvalidDataException(
                        "Package success exists without the complete progress set.");
                ValidateTag(
                    progress,
                    context.SourceRevision,
                    CreatePackageAnnotation(context, package));
                terminalCompleted.Add(package.PackageId);
            }

            _terminalWithoutPending = true;
            return terminalCompleted;
        }

        await _tags.CreateOrValidateAsync(
                PendingName(context.Version),
                context.SourceRevision,
                _pendingAnnotation,
                cancellationToken)
            .ConfigureAwait(false);
        var completed = new List<string>();

        foreach (var package in context.Packages)
        {
            var tag = await _tags.ReadAsync(
                    PackageName(context.Version, package.PackageId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (tag is null)
            {
                continue;
            }

            ValidateTag(
                tag,
                context.SourceRevision,
                CreatePackageAnnotation(context, package));
            completed.Add(package.PackageId);
        }

        var success = await _tags.ReadAsync(
                context.Version,
                cancellationToken)
            .ConfigureAwait(false);

        if (success is not null)
        {
            ValidateTag(
                success,
                context.SourceRevision,
                CreateSuccessAnnotation(context));

            if (completed.Count != context.Packages.Count)
            {
                throw new InvalidDataException(
                    "Package success exists without the complete progress set.");
            }
        }

        return completed;
    }

    /// <inheritdoc/>
    public Task RecordCompletedAsync(
        PackagePublicationCheckpointContext context,
        PackagePublicationCheckpointRecord package,
        CancellationToken cancellationToken)
    {
        RequireSameContext(context);
        return _tags.CreateOrValidateAsync(
            PackageName(context.Version, package.PackageId),
            context.SourceRevision,
            CreatePackageAnnotation(context, package),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(
        PackagePublicationCheckpointContext context,
        CancellationToken cancellationToken)
    {
        RequireSameContext(context);

        foreach (var package in context.Packages)
        {
            var tag = await _tags.ReadAsync(
                    PackageName(context.Version, package.PackageId),
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException(
                    "Package completion requires every progress tag.");
            ValidateTag(
                tag,
                context.SourceRevision,
                CreatePackageAnnotation(context, package));
        }

        await _tags.CreateOrValidateAsync(
                context.Version,
                context.SourceRevision,
                CreateSuccessAnnotation(context),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the matching pending reservation after provider publication.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    public Task CleanupPendingAsync(CancellationToken cancellationToken)
    {
        var context = _context
            ?? throw new InvalidOperationException(
                "The package checkpoint has not started.");
        return _terminalWithoutPending
            ? Task.CompletedTask
            : _tags.DeleteMatchingAsync(
            PendingName(context.Version),
            context.SourceRevision,
            _pendingAnnotation!,
            cancellationToken);
    }

    private async Task ValidateExpectedProgressAsync(
        string version,
        GitTagSnapshot anchor,
        IReadOnlyDictionary<string, string> anchorFields,
        CancellationToken cancellationToken)
    {
        foreach (var packageId in ExpectedPackageIds)
        {
            var progress = await _tags.ReadAsync(
                    PackageName(version, packageId),
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException(
                    "Terminal lifecycle evidence lacks package progress.");
            var fields = ParseExact(
                progress,
                PackageHeader,
                "package-id",
                "package-sha256",
                "symbol-sha256",
                "manifest-sha256",
                "run-identity");
            ValidateCommonFields(fields);
            ValidateHash(fields["package-sha256"]);

            if (!fields["symbol-sha256"].Equals(
                    "none",
                    StringComparison.Ordinal))
            {
                ValidateHash(fields["symbol-sha256"]);
            }

            if (!fields["package-id"].Equals(
                    packageId,
                    StringComparison.Ordinal)
                || !progress.TargetRevision.Equals(
                    anchor.TargetRevision,
                    StringComparison.OrdinalIgnoreCase)
                || !fields["manifest-sha256"].Equals(
                    anchorFields["manifest-sha256"],
                    StringComparison.Ordinal)
                || !fields["run-identity"].Equals(
                    anchorFields["run-identity"],
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Package progress conflicts with terminal evidence.");
            }
        }
    }

    private async Task ValidatePresentProgressAsync(
        string version,
        GitTagSnapshot anchor,
        IReadOnlyDictionary<string, string> anchorFields,
        CancellationToken cancellationToken)
    {
        foreach (var packageId in ExpectedPackageIds)
        {
            var progress = await _tags.ReadAsync(
                    PackageName(version, packageId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (progress is null)
            {
                continue;
            }

            var fields = ParseExact(
                progress,
                PackageHeader,
                "package-id",
                "package-sha256",
                "symbol-sha256",
                "manifest-sha256",
                "run-identity");
            ValidateCommonFields(fields);
            ValidateHash(fields["package-sha256"]);

            if (!fields["symbol-sha256"].Equals(
                    "none",
                    StringComparison.Ordinal))
            {
                ValidateHash(fields["symbol-sha256"]);
            }

            if (!fields["package-id"].Equals(
                    packageId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Package progress has a conflicting identity.");
            }

            RequireMatching(anchor, progress, anchorFields, fields);
        }
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

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

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
                || !values.TryAdd(expected, line[(separator + 1)..]))
            {
                throw new InvalidDataException(
                    $"Lifecycle tag '{tag.Name}' has an invalid field.");
            }
        }

        return values;
    }

    private static void RequireMatching(
        GitTagSnapshot left,
        GitTagSnapshot right,
        IReadOnlyDictionary<string, string> leftFields,
        IReadOnlyDictionary<string, string> rightFields)
    {
        if (left.TargetRevision.Equals(
                right.TargetRevision,
                StringComparison.OrdinalIgnoreCase)
            && leftFields["manifest-sha256"].Equals(
                rightFields["manifest-sha256"],
                StringComparison.Ordinal)
            && leftFields["run-identity"].Equals(
                rightFields["run-identity"],
                StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            "Lifecycle evidence fields conflict.");
    }

    private static void ValidateCommonFields(
        IReadOnlyDictionary<string, string> fields)
    {
        ValidateHash(fields["manifest-sha256"]);
        ValidateRunIdentity(fields["run-identity"]);
    }

    private static void ValidateHash(string value)
    {
        if (value.Length != 64 || !value.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException(
                "Lifecycle evidence contains an invalid SHA-256.");
        }
    }

    private static void ValidateRunIdentity(string value)
    {
        const string Prefix = "github:";

        if (!value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Length == Prefix.Length
            || !value[Prefix.Length..].All(char.IsAsciiDigit))
        {
            throw new InvalidDataException(
                "Lifecycle evidence contains an invalid run identity.");
        }
    }

    private static void ValidateOpaque(string value)
    {
        if (value.Length is < 1 or > 128
            || !char.IsAsciiLetterOrDigit(value[0])
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not ('.' or '_' or '-')))
        {
            throw new InvalidDataException(
                "Lifecycle evidence contains an invalid audit reference.");
        }
    }

    private static string PendingName(string version)
    {
        return $"nuget-pending/{version}";
    }

    private static string PackageName(string version, string packageId)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"nuget-package/{version}/{packageId.ToLowerInvariant()}");
    }

    private string CreatePendingAnnotation(
        PackagePublicationCheckpointContext context)
    {
        return string.Join(
            '\n',
            PendingHeader,
            $"manifest-sha256={context.ManifestSha256}",
            $"run-identity={runIdentity}");
    }

    private string CreatePackageAnnotation(
        PackagePublicationCheckpointContext context,
        PackagePublicationCheckpointRecord package)
    {
        return string.Join(
            '\n',
            PackageHeader,
            $"package-id={package.PackageId}",
            $"package-sha256={package.PackageSha256}",
            $"symbol-sha256={package.SymbolSha256 ?? "none"}",
            $"manifest-sha256={context.ManifestSha256}",
            $"run-identity={runIdentity}");
    }

    private string CreateSuccessAnnotation(
        PackagePublicationCheckpointContext context)
    {
        return string.Join(
            '\n',
            SuccessHeader,
            $"manifest-sha256={context.ManifestSha256}",
            $"run-identity={runIdentity}");
    }

    private void ValidateContext(
        PackagePublicationCheckpointContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Version)
            || string.IsNullOrWhiteSpace(context.SourceRevision)
            || context.ManifestSha256.Length != 64
            || !context.ManifestSha256.All(char.IsAsciiHexDigit)
            || context.Packages.Count == 0
            || context.Packages.Select(package => package.PackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != context.Packages.Count
            || context.Packages.Count != ExpectedPackageIds.Length
            || ExpectedPackageIds.Any(expected =>
                !context.Packages.Any(package =>
                    package.PackageId.Equals(
                        expected,
                        StringComparison.Ordinal)))
            || context.Packages.Any(package =>
                package.PackageSha256.Length != 64
                || !package.PackageSha256.All(char.IsAsciiHexDigit)
                || package.SymbolSha256 is not null
                && (package.SymbolSha256.Length != 64
                    || !package.SymbolSha256.All(char.IsAsciiHexDigit))))
        {
            throw new InvalidDataException(
                "The package checkpoint context is invalid.");
        }

        ValidateRunIdentity(runIdentity);
    }

    private void RequireSameContext(
        PackagePublicationCheckpointContext context)
    {
        if (ReferenceEquals(_context, context))
        {
            return;
        }

        throw new InvalidOperationException(
            "The package checkpoint context changed during publication.");
    }

    private static void ValidateTag(
        GitTagSnapshot tag,
        string revision,
        string annotation)
    {
        if (tag.ObjectType.Equals("tag", StringComparison.Ordinal)
            && tag.TargetRevision.Equals(
                revision,
                StringComparison.OrdinalIgnoreCase)
            && tag.Annotation.Equals(annotation, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            $"Lifecycle tag '{tag.Name}' is conflicting or malformed.");
    }
}
