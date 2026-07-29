namespace TedToolkit.Build.ReleaseLifecycle;

/// <summary>
/// Reads and mutates immutable annotated lifecycle tags.
/// </summary>
internal sealed class GitTagStore(DirectoryInfo repositoryRoot)
{
    private readonly GitCommandRunner _git = new(repositoryRoot);

    /// <summary>
    /// Reads every local tag from the complete checkout.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    /// <returns>The exact local tag snapshots.</returns>
    public async Task<IReadOnlyList<GitTagSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        var names = (await _git.RequireAsync(
                ["tag", "--list"],
                null,
                cancellationToken)
            .ConfigureAwait(false))
            .Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
        var tags = new List<GitTagSnapshot>(names.Length);

        foreach (var name in names)
        {
            tags.Add(await ReadRequiredAsync(name, cancellationToken)
                .ConfigureAwait(false));
        }

        return tags;
    }

    /// <summary>
    /// Reads one tag when it exists.
    /// </summary>
    /// <param name="name">The short tag name.</param>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    /// <returns>The tag snapshot, or <see langword="null"/>.</returns>
    public async Task<GitTagSnapshot?> ReadAsync(
        string name,
        CancellationToken cancellationToken)
    {
        ValidateName(name);
        var exists = await _git.RunAsync(
                ["show-ref", "--verify", "--quiet", $"refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return exists.ExitCode switch
        {
            0 => await ReadRequiredAsync(name, cancellationToken)
                .ConfigureAwait(false),
            1 => null,
            _ => throw new InvalidOperationException(
                "Unable to inspect a lifecycle tag."),
        };
    }

    /// <summary>
    /// Creates an immutable annotated tag or validates an equal existing tag.
    /// </summary>
    /// <param name="name">The short tag name.</param>
    /// <param name="targetRevision">The exact commit target.</param>
    /// <param name="annotation">The exact LF-delimited annotation.</param>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    public async Task CreateOrValidateAsync(
        string name,
        string targetRevision,
        string annotation,
        CancellationToken cancellationToken)
    {
        ValidateName(name);
        ValidateRevision(targetRevision);
        ValidateAnnotation(annotation);
        var existing = await ReadAsync(name, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            ValidateEqual(existing, targetRevision, annotation);
            return;
        }

        await _git.RequireAsync(
                ["tag", "--annotate", name, targetRevision, "--file", "-"],
                annotation,
                cancellationToken)
            .ConfigureAwait(false);
        var push = await _git.RunAsync(
                ["push", "origin", $"refs/tags/{name}:refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (push.ExitCode == 0)
        {
            return;
        }

        await _git.RequireAsync(
                ["fetch", "--force", "origin", $"refs/tags/{name}:refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);
        ValidateEqual(
            await ReadRequiredAsync(name, cancellationToken)
                .ConfigureAwait(false),
            targetRevision,
            annotation);
    }

    /// <summary>
    /// Deletes a matching pending tag from the remote and local checkout.
    /// </summary>
    /// <param name="name">The pending tag name.</param>
    /// <param name="targetRevision">The expected target.</param>
    /// <param name="annotation">The expected annotation.</param>
    /// <param name="cancellationToken">A token that cancels Git.</param>
    public async Task DeleteMatchingAsync(
        string name,
        string targetRevision,
        string annotation,
        CancellationToken cancellationToken)
    {
        var existing = await ReadAsync(name, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        ValidateEqual(existing, targetRevision, annotation);
        await _git.RequireAsync(
                ["push", "origin", "--delete", $"refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false);
        await _git.RequireAsync(
                ["tag", "--delete", name],
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<GitTagSnapshot> ReadRequiredAsync(
        string name,
        CancellationToken cancellationToken)
    {
        ValidateName(name);
        var type = (await _git.RequireAsync(
                ["cat-file", "-t", $"refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false)).Trim();
        var target = (await _git.RequireAsync(
                ["rev-list", "-n", "1", $"refs/tags/{name}"],
                null,
                cancellationToken)
            .ConfigureAwait(false)).Trim();
        var annotation = type.Equals("tag", StringComparison.Ordinal)
            ? (await _git.RequireAsync(
                    ["for-each-ref", "--format=%(contents)", $"refs/tags/{name}"],
                    null,
                    cancellationToken)
                .ConfigureAwait(false)).ReplaceLineEndings("\n").TrimEnd('\n')
            : "";
        return new(name, target, type, annotation);
    }

    private void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Any(char.IsControl))
        {
            throw new InvalidDataException("A lifecycle tag name is invalid.");
        }

        var result = _git.RunAsync(
                ["check-ref-format", $"refs/tags/{name}"],
                null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (result.ExitCode != 0)
        {
            throw new InvalidDataException("A lifecycle tag name is invalid.");
        }
    }

    private static void ValidateRevision(string revision)
    {
        if (revision.Length is >= 7 and <= 64
            && revision.All(char.IsAsciiHexDigit))
        {
            return;
        }

        throw new InvalidDataException("A lifecycle target revision is invalid.");
    }

    private static void ValidateAnnotation(string annotation)
    {
        if (!string.IsNullOrWhiteSpace(annotation)
            && !annotation.Contains('\r', StringComparison.Ordinal)
            && !annotation.Contains('\0', StringComparison.Ordinal)
            && System.Text.Encoding.UTF8.GetByteCount(annotation) <= 32 * 1024)
        {
            return;
        }

        throw new InvalidDataException("A lifecycle annotation is invalid.");
    }

    private static void ValidateEqual(
        GitTagSnapshot existing,
        string targetRevision,
        string annotation)
    {
        if (existing.ObjectType.Equals("tag", StringComparison.Ordinal)
            && existing.TargetRevision.Equals(
                targetRevision,
                StringComparison.OrdinalIgnoreCase)
            && existing.Annotation.Equals(
                annotation,
                StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            $"Lifecycle tag '{existing.Name}' conflicts with the requested immutable evidence.");
    }
}

/// <summary>
/// Represents one local Git tag and its peeled target.
/// </summary>
/// <param name="Name">The exact short tag name.</param>
/// <param name="TargetRevision">The peeled commit target.</param>
/// <param name="ObjectType">The Git object type at the ref.</param>
/// <param name="Annotation">The normalized annotated-tag message.</param>
internal sealed record GitTagSnapshot(
    string Name,
    string TargetRevision,
    string ObjectType,
    string Annotation);