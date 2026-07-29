namespace TedToolkit.Build.ReleaseLifecycle;

/// <summary>
/// Performs audited host-only abandonment or Release-finalization recovery.
/// </summary>
internal sealed class ReleaseRecoveryCoordinator(
    DirectoryInfo repositoryRoot)
{
    private static readonly string[] ExpectedPackageIds =
    [
        "TedToolkit.CodeAnalysis",
        "TedToolkit.ModularPipelines.Build",
        "TedToolkit.ModularPipelines.Combine",
    ];

    private readonly GitTagStore _tags = new(repositoryRoot);

    /// <summary>
    /// Converts an active pre-success reservation into immutable abandonment.
    /// </summary>
    /// <param name="version">The normalized reserved version.</param>
    /// <param name="auditReference">The opaque operator audit reference.</param>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    /// <returns>The terminal disposition.</returns>
    public async Task<string> AbandonAsync(
        string version,
        string auditReference,
        CancellationToken cancellationToken)
    {
        ValidateInputs(version, auditReference);
        var pendingName = $"nuget-pending/{version}";
        var abandonedName = $"nuget-abandoned/{version}";
        var pending = await _tags.ReadAsync(pendingName, cancellationToken)
            .ConfigureAwait(false);
        var success = await _tags.ReadAsync(version, cancellationToken)
            .ConfigureAwait(false);
        var finalized = await _tags.ReadAsync(
                $"nuget-finalized/{version}",
                cancellationToken)
            .ConfigureAwait(false);
        var abandoned = await _tags.ReadAsync(
                abandonedName,
                cancellationToken)
            .ConfigureAwait(false);

        if (success is not null || finalized is not null)
        {
            throw new InvalidDataException(
                "A package-success version cannot be abandoned.");
        }

        if (pending is null)
        {
            if (abandoned is null)
            {
                throw new InvalidDataException(
                    "Abandonment requires pending lifecycle evidence.");
            }

            _ = ParseExact(
                abandoned,
                "tedtoolkit-nuget-abandoned-v1",
                "manifest-sha256",
                "run-identity",
                "audit-reference");
            return "already-abandoned";
        }

        var fields = ParseExact(
            pending,
            "tedtoolkit-nuget-pending-v1",
            "manifest-sha256",
            "run-identity");
        var annotation = string.Join(
            '\n',
            "tedtoolkit-nuget-abandoned-v1",
            $"manifest-sha256={fields["manifest-sha256"]}",
            $"run-identity={fields["run-identity"]}",
            $"audit-reference={auditReference}");
        await _tags.CreateOrValidateAsync(
                abandonedName,
                pending.TargetRevision,
                annotation,
                cancellationToken)
            .ConfigureAwait(false);
        await _tags.DeleteMatchingAsync(
                pendingName,
                pending.TargetRevision,
                pending.Annotation,
                cancellationToken)
            .ConfigureAwait(false);
        return abandoned is null ? "abandoned" : "abandoned-cleaned";
    }

    /// <summary>
    /// Finalizes a package-success Release, records audit evidence, then removes
    /// the matching pending reservation.
    /// </summary>
    /// <param name="version">The normalized reserved version.</param>
    /// <param name="auditReference">The opaque operator audit reference.</param>
    /// <param name="recoveryRunIdentity">The current recovery run identity.</param>
    /// <param name="finalizeRelease">
    /// The provider-neutral Release-only operation.
    /// </param>
    /// <param name="cancellationToken">A token that cancels recovery.</param>
    /// <returns>The terminal disposition.</returns>
    public async Task<string> FinalizeAsync(
        string version,
        string auditReference,
        string recoveryRunIdentity,
        Func<string, CancellationToken, Task> finalizeRelease,
        CancellationToken cancellationToken)
    {
        ValidateInputs(version, auditReference);
        ValidateRunIdentity(recoveryRunIdentity);
        ArgumentNullException.ThrowIfNull(finalizeRelease);
        var pendingName = $"nuget-pending/{version}";
        var finalizedName = $"nuget-finalized/{version}";
        var pending = await _tags.ReadAsync(pendingName, cancellationToken)
            .ConfigureAwait(false);
        var success = await _tags.ReadAsync(version, cancellationToken)
            .ConfigureAwait(false);
        var abandoned = await _tags.ReadAsync(
                $"nuget-abandoned/{version}",
                cancellationToken)
            .ConfigureAwait(false);
        var finalized = await _tags.ReadAsync(
                finalizedName,
                cancellationToken)
            .ConfigureAwait(false);

        if (abandoned is not null || success is null)
        {
            throw new InvalidDataException(
                "Release finalization requires unambiguous package-success evidence.");
        }

        var successFields = ParseExact(
            success,
            "tedtoolkit-nuget-success-v1",
            "manifest-sha256",
            "run-identity");

        if (pending is null)
        {
            if (finalized is not null)
            {
                ValidateFinalized(
                    finalized,
                    success,
                    successFields,
                    auditReference);
                return "already-finalized";
            }

            return "already-published";
        }

        var pendingFields = ParseExact(
            pending,
            "tedtoolkit-nuget-pending-v1",
            "manifest-sha256",
            "run-identity");
        RequireMatchingEvidence(pending, success, pendingFields, successFields);
        await ValidateProgressAsync(
                version,
                success,
                successFields,
                cancellationToken)
            .ConfigureAwait(false);

        if (finalized is null)
        {
            await finalizeRelease(
                    success.TargetRevision,
                    cancellationToken)
                .ConfigureAwait(false);
            var annotation = string.Join(
                '\n',
                "tedtoolkit-nuget-finalized-v1",
                $"manifest-sha256={successFields["manifest-sha256"]}",
                $"run-identity={successFields["run-identity"]}",
                $"recovery-run-identity={recoveryRunIdentity}",
                $"audit-reference={auditReference}");
            await _tags.CreateOrValidateAsync(
                    finalizedName,
                    success.TargetRevision,
                    annotation,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            ValidateFinalized(
                finalized,
                success,
                successFields,
                auditReference);
        }

        await _tags.DeleteMatchingAsync(
                pendingName,
                pending.TargetRevision,
                pending.Annotation,
                cancellationToken)
            .ConfigureAwait(false);
        return finalized is null ? "finalized" : "finalized-cleaned";
    }

    private async Task ValidateProgressAsync(
        string version,
        GitTagSnapshot success,
        IReadOnlyDictionary<string, string> successFields,
        CancellationToken cancellationToken)
    {
        foreach (var packageId in ExpectedPackageIds)
        {
            var tag = await _tags.ReadAsync(
                    $"nuget-package/{version}/{packageId.ToLowerInvariant()}",
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException(
                    "Release finalization requires the complete package-progress set.");
            var fields = ParseExact(
                tag,
                "tedtoolkit-nuget-package-v1",
                "package-id",
                "package-sha256",
                "symbol-sha256",
                "manifest-sha256",
                "run-identity");

            if (!fields["package-id"].Equals(
                    packageId,
                    StringComparison.Ordinal)
                || !tag.TargetRevision.Equals(
                    success.TargetRevision,
                    StringComparison.OrdinalIgnoreCase)
                || !fields["manifest-sha256"].Equals(
                    successFields["manifest-sha256"],
                    StringComparison.Ordinal)
                || !fields["run-identity"].Equals(
                    successFields["run-identity"],
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Package-progress evidence conflicts with package success.");
            }
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

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < expectedFields.Length; index++)
        {
            var separator = lines[index + 1].IndexOf('=');
            var expected = expectedFields[index];

            if (separator <= 0
                || !lines[index + 1][..separator].Equals(
                    expected,
                    StringComparison.Ordinal)
                || string.IsNullOrEmpty(lines[index + 1][(separator + 1)..])
                || !result.TryAdd(
                    expected,
                    lines[index + 1][(separator + 1)..]))
            {
                throw new InvalidDataException(
                    $"Lifecycle tag '{tag.Name}' has an invalid field.");
            }
        }

        return result;
    }

    private static void ValidateFinalized(
        GitTagSnapshot finalized,
        GitTagSnapshot success,
        IReadOnlyDictionary<string, string> successFields,
        string auditReference)
    {
        var fields = ParseExact(
            finalized,
            "tedtoolkit-nuget-finalized-v1",
            "manifest-sha256",
            "run-identity",
            "recovery-run-identity",
            "audit-reference");

        if (!finalized.TargetRevision.Equals(
                success.TargetRevision,
                StringComparison.OrdinalIgnoreCase)
            || !fields["manifest-sha256"].Equals(
                successFields["manifest-sha256"],
                StringComparison.Ordinal)
            || !fields["run-identity"].Equals(
                successFields["run-identity"],
                StringComparison.Ordinal)
            || !fields["audit-reference"].Equals(
                auditReference,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Finalized audit evidence conflicts with package success.");
        }

        ValidateRunIdentity(fields["recovery-run-identity"]);
    }

    private static void RequireMatchingEvidence(
        GitTagSnapshot pending,
        GitTagSnapshot success,
        IReadOnlyDictionary<string, string> pendingFields,
        IReadOnlyDictionary<string, string> successFields)
    {
        if (pending.TargetRevision.Equals(
                success.TargetRevision,
                StringComparison.OrdinalIgnoreCase)
            && pendingFields["manifest-sha256"].Equals(
                successFields["manifest-sha256"],
                StringComparison.Ordinal)
            && pendingFields["run-identity"].Equals(
                successFields["run-identity"],
                StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            "Pending evidence conflicts with package success.");
    }

    private static void ValidateInputs(
        string version,
        string auditReference)
    {
        if (string.IsNullOrWhiteSpace(version)
            || !IsOpaqueIdentifier(auditReference))
        {
            throw new InvalidDataException(
                "Recovery version or audit reference is invalid.");
        }
    }

    private static bool IsOpaqueIdentifier(string value)
    {
        return value.Length is >= 1 and <= 128
               && char.IsAsciiLetterOrDigit(value[0])
               && value.All(character =>
                   char.IsAsciiLetterOrDigit(character)
                   || character is '.' or '_' or '-');
    }

    private static void ValidateRunIdentity(string value)
    {
        const string Prefix = "github:";

        if (value.StartsWith(Prefix, StringComparison.Ordinal)
            && value.Length > Prefix.Length
            && value[Prefix.Length..].All(char.IsAsciiDigit))
        {
            return;
        }

        throw new InvalidDataException(
            "The recovery run identity is invalid.");
    }
}