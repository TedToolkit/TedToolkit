// -----------------------------------------------------------------------
// <copyright file="CombineOptionsValidator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Validates the complete profile, input, action, and extension matrix.
/// </summary>
internal static class CombineOptionsValidator
{
    /// <summary>
    /// Validates composition before callbacks or side-effect boundaries run.
    /// </summary>
    /// <param name="rootDirectory">The explicit consumer root.</param>
    /// <param name="options">The authoritative Combine options.</param>
    /// <param name="buildInputs">The optional RunBuild inputs.</param>
    /// <param name="buildOptions">The shared Build options.</param>
    /// <param name="registrations">Trusted consumer extensions.</param>
    /// <returns>The immutable validated registration.</returns>
    /// <exception cref="ArgumentNullException">
    /// A required composition value is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The composition is inconsistent or unsafe.
    /// </exception>
    public static ValidatedCombineRegistration Validate(
        DirectoryInfo rootDirectory,
        CombineOptions options,
        BuildInputs? buildInputs,
        BuildOptions buildOptions,
        IEnumerable<PipelineModuleRegistration>? registrations)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(buildOptions);
        var rootPath = Path.GetFullPath(rootDirectory.ToString());

        if (!Path.IsPathFullyQualified(rootDirectory.ToString())
            || !Directory.Exists(rootPath))
        {
            throw new InvalidDataException(
                "The Combine root must be an existing absolute directory.");
        }

        var registrationArray = registrations?.ToArray()
            ?? Array.Empty<global::TedToolkit.ModularPipelines.Combine.Configuration.PipelineModuleRegistration>();
        ValidateRegistrations(registrationArray);
        var resolved = StandardProfileResolver.Resolve(
            options,
            buildOptions.Conventions);
        ValidateActions(resolved.Profile, options.Actions);

        if (resolved.Profile == PipelineProfile.None)
        {
            return new(
                rootDirectory,
                rootPath,
                resolved.Profile,
                resolved.Context,
                options,
                buildInputs,
                buildOptions,
                registrationArray,
                null);
        }

        ValidateInputMode(
            rootPath,
            resolved.Profile,
            options,
            buildInputs,
            registrationArray);
        ValidateCommits(options.Commits);
        ValidateNotifications(options.Actions);
        ValidateNuGet(options);
        ValidateProviderActions(options);

        string? manifestPath;
        if (options.InputMode
                    == PipelineInputMode.ConsumeArtifacts)
        {
            manifestPath = ResolveManifest(rootPath, options.ArtifactManifestPath!);
        }
        else if (buildInputs is null)
        {
            manifestPath = null;
        }
        else
        {
            manifestPath = Path.Combine(
                            buildInputs.ArtifactRootDirectory.FullName,
                            buildOptions.Conventions.Layout.ManifestFileName);
        }

        return new(
            rootDirectory,
            rootPath,
            resolved.Profile,
            resolved.Context,
            options,
            buildInputs,
            buildOptions,
            registrationArray,
            manifestPath);
    }

    private static void ValidateCommits(
        IReadOnlyList<CommitSummary>? commits)
    {
        if (commits is null)
        {
            throw new InvalidDataException(
                "Pipeline runtime commit summaries cannot be null.");
        }

        foreach (var commit in commits)
        {
            if (commit is null
                || commit.Sha.Length is not (40 or 64)
                || !commit.Sha.All(Uri.IsHexDigit)
                || string.IsNullOrWhiteSpace(commit.Subject)
                || commit.Subject.Any(char.IsControl)
                || commit.Subject.Length > 512
                || (commit.Uri is not null
                && (!commit.Uri.IsAbsoluteUri
                    || commit.Uri.Scheme != Uri.UriSchemeHttps
                    || !string.IsNullOrEmpty(commit.Uri.UserInfo)
                    || !string.IsNullOrEmpty(commit.Uri.Query)
                    || !string.IsNullOrEmpty(commit.Uri.Fragment))))
            {
                throw new InvalidDataException(
                    "Pipeline runtime commit summary is invalid.");
            }
        }
    }

    private static void ValidateRegistrations(
        IReadOnlyList<PipelineModuleRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            if (registration is null
                || registration.Profiles is null
                || registration.Profiles.Count == 0
                || registration.Profiles.Contains(PipelineProfile.None)
                || registration.Profiles.Any(profile =>
                    !Enum.IsDefined(profile))
                || !Enum.IsDefined(registration.Kind)
                || registration.Configure is null)
            {
                throw new InvalidDataException(
                    "Pipeline module registration metadata is invalid.");
            }

            if (registration.WritesRepositoryFiles
                && registration.Profiles.Any(profile =>
                    profile != PipelineProfile.LocalBuild))
            {
                throw new InvalidDataException(
                    "Repository-writing extensions are LocalBuild-only.");
            }
        }
    }

    private static void ValidateInputMode(
        string rootPath,
        PipelineProfile profile,
        CombineOptions options,
        BuildInputs? buildInputs,
        IReadOnlyList<PipelineModuleRegistration> registrations)
    {
        if (!Enum.IsDefined(options.InputMode))
        {
            throw new InvalidDataException(
                "Pipeline:InputMode is invalid.");
        }

        if (options.InputMode == PipelineInputMode.RunBuild)
        {
            if (buildInputs is null
                || options.ArtifactManifestPath is not null)
            {
                throw new InvalidDataException(
                    "RunBuild requires BuildInputs and forbids ArtifactManifestPath.");
            }

            if (!string.Equals(
                    Path.GetFullPath(buildInputs.RootDirectory.ToString()),
                    rootPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "BuildInputs.RootDirectory must equal the Combine root.");
            }

            return;
        }

        if (profile is PipelineProfile.LocalBuild or PipelineProfile.Pack)
        {
            throw new InvalidDataException(
                $"Profile {profile} forbids ConsumeArtifacts.");
        }

        if (buildInputs is null
            && !string.IsNullOrWhiteSpace(options.ArtifactManifestPath)
            && !registrations.Any(registration =>
                registration.Kind == PipelineExtensionKind.BuildStage))
        {
            return;
        }

        throw new InvalidDataException(
            "ConsumeArtifacts requires only ArtifactManifestPath and forbids Build stages.");
    }

    private static void ValidateActions(
        PipelineProfile profile,
        PipelineActionOptions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [nameof(actions.PushNuGetPackages)] = actions.PushNuGetPackages,
            [nameof(actions.PublishArtifacts)] = actions.PublishArtifacts,
            [nameof(actions.CreateChangeRequest)] =
                actions.CreateChangeRequest,
            [nameof(actions.CreateRelease)] = actions.CreateRelease,
            [nameof(actions.EmitNotifications)] = actions.EmitNotifications,
        };
        var allowed = profile switch
        {
            PipelineProfile.Publish => new HashSet<string>(
            [
                nameof(actions.PushNuGetPackages),
                nameof(actions.PublishArtifacts),
                nameof(actions.CreateRelease),
                nameof(actions.EmitNotifications),
            ], StringComparer.Ordinal),
            PipelineProfile.Message => new HashSet<string>(
            [
                nameof(actions.CreateChangeRequest),
                nameof(actions.EmitNotifications),
            ], StringComparer.Ordinal),
            PipelineProfile.None => [],
            _ => new HashSet<string>(
            [
                nameof(actions.EmitNotifications),
            ], StringComparer.Ordinal),
        };

        var forbiddenAction = enabled.FirstOrDefault(item =>
            item.Value && !allowed.Contains(item.Key));

        if (string.IsNullOrEmpty(forbiddenAction.Key))
        {
            return;
        }

        throw new InvalidDataException(
            $"Profile {profile} forbids action {forbiddenAction.Key}.");
    }

    private static void ValidateNotifications(PipelineActionOptions actions)
    {
        if (Enum.IsDefined(actions.NotificationFailureMode)
            && actions.NotificationTimeout >= TimeSpan.FromSeconds(1)
            && actions.NotificationTimeout <= TimeSpan.FromMinutes(10))
        {
            return;
        }

        throw new InvalidDataException(
            "Pipeline notification configuration is invalid.");
    }

    private static void ValidateNuGet(CombineOptions options)
    {
        if (!options.Actions.PushNuGetPackages)
        {
            return;
        }

        var push = options.NuGetPush
            ?? throw new InvalidDataException(
                "Pipeline:NuGetPush is required for PushNuGetPackages.");

        if (!Enum.IsDefined(push.AuthenticationMode)
            || push.Timeout < TimeSpan.FromSeconds(1)
            || push.Timeout > TimeSpan.FromHours(1))
        {
            throw new InvalidDataException(
                "Pipeline:NuGetPush configuration is invalid.");
        }

        ValidateSource(push.Source, push.AllowInsecureHttp, "Source");
        ValidateSource(
            push.SymbolSource,
            push.AllowInsecureHttp,
            "SymbolSource",
            optional: true);

        if (push.AuthenticationMode == NuGetAuthenticationMode.ApiKey)
        {
            ValidateReference(
                push.CredentialReference,
                "NuGetPush:CredentialReference");
            if (push.SymbolCredentialReference is not null)
            {
                ValidateReference(
                    push.SymbolCredentialReference,
                    "NuGetPush:SymbolCredentialReference");
            }

            if (push.ConfigFilePath is not null)
            {
                throw new InvalidDataException(
                    "ApiKey authentication forbids ConfigFilePath.");
            }
        }
        else if (push.CredentialReference is not null
                 || push.SymbolCredentialReference is not null)
        {
            throw new InvalidDataException(
                "NuGetConfig authentication forbids credential references.");
        }

        if (push.UsePackagePublicationCheckpoint
            && options.PackagePublicationCheckpoint is null)
        {
            throw new InvalidDataException(
                "Package checkpointing requires exactly one trusted implementation.");
        }

        if (!push.UsePackagePublicationCheckpoint || !push.SkipDuplicate)
        {
            return;
        }

        throw new InvalidDataException(
            "Package checkpointing requires SkipDuplicate=false.");
    }

    private static void ValidateProviderActions(CombineOptions options)
    {
        var requiresProvider = options.Actions.PublishArtifacts
                               || options.Actions.CreateChangeRequest
                               || options.Actions.CreateRelease;

        if (!requiresProvider)
        {
            return;
        }

        if (options.Actions.CreateChangeRequest)
        {
            ValidateChangeRequest(options.ChangeRequest);
        }

        if (options.RepositoryProvider is null)
        {
            throw new InvalidDataException(
                "Pipeline:RepositoryProvider is required by the enabled action.");
        }

        if (options.RepositoryProvider == RepositoryProviderKind.GitHub)
        {
            if (options.GitHub is null || options.GitLab is not null)
            {
                throw new InvalidDataException(
                    "Exactly the selected GitHub connection is required.");
            }

            _ = ProviderConnectionResolver.ResolveGitHub(options.GitHub);
        }
        else if (options.RepositoryProvider == RepositoryProviderKind.GitLab)
        {
            if (options.GitLab is null || options.GitHub is not null)
            {
                throw new InvalidDataException(
                    "Exactly the selected GitLab connection is required.");
            }

            _ = ProviderConnectionResolver.ResolveGitLab(options.GitLab);
        }
        else
        {
            throw new InvalidDataException(
                "Pipeline:RepositoryProvider is invalid.");
        }

        if (!options.Actions.PublishArtifacts && !options.Actions.CreateRelease)
        {
            return;
        }

        var publication = options.Publication
            ?? throw new InvalidDataException(
                "Pipeline:Publication is required by the enabled action.");
        var version = NuGetVersion.Parse(
            publication.Version
            ?? throw new InvalidDataException(
                "Pipeline:Publication:Version is required."));

        if (!string.Equals(
                version.ToNormalizedString(),
                publication.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Pipeline:Publication:Version must be normalized.");
        }

        if (!options.Actions.CreateRelease)
        {
            return;
        }

        ValidateBoundedText(
            publication.TagName,
            "Publication:TagName");
        ValidateBoundedText(
            publication.Title,
            "Publication:Title");
        var body = publication.Body.ReplaceLineEndings("\n");

        if (!body.Contains('\0', StringComparison.Ordinal)
            && System.Text.Encoding.UTF8.GetByteCount(body)
            <= 1024 * 1024)
        {
            return;
        }

        throw new InvalidDataException(
            "Pipeline:Publication:Body is invalid.");
    }

    private static string ResolveManifest(
        string rootPath,
        string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidDataException(
                "ArtifactManifestPath must be relative to the Combine root.");
        }

        var fullPath = Path.GetFullPath(
            Path.Combine(rootPath, relativePath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = Path.TrimEndingDirectorySeparator(rootPath)
                         + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidDataException(
                "ArtifactManifestPath escapes the Combine root.");
        }

        return fullPath;
    }

    private static void ValidateSource(
        Uri? value,
        bool allowInsecureHttp,
        string name,
        bool optional = false)
    {
        if (value is null)
        {
            if (optional)
            {
                return;
            }

            throw new InvalidDataException(
                $"Pipeline:NuGetPush:{name} is required.");
        }

        if (value.IsAbsoluteUri
            && string.IsNullOrEmpty(value.UserInfo)
            && string.IsNullOrEmpty(value.Query)
            && string.IsNullOrEmpty(value.Fragment)
            && (value.Scheme == Uri.UriSchemeHttps
            || (allowInsecureHttp && value.Scheme == Uri.UriSchemeHttp)))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:NuGetPush:{name} is invalid.");
    }

    private static void ValidateReference(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && !value.Any(char.IsControl))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:{path} is invalid.");
    }

    private static void ValidateBoundedText(string? value, string path)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.Trim().Length <= 256
            && !value.Any(char.IsControl))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:{path} is invalid.");
    }

    private static void ValidateChangeRequest(ChangeRequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ReleaseTitle)
            && options.ReleaseTitle.Equals(
                options.ReleaseTitle.Trim(),
                StringComparison.Ordinal)
            && !options.ReleaseTitle.Any(char.IsControl)
            && options.ReleaseTitle.Length <= 256
            && options.Labels?.All(label =>
                !string.IsNullOrWhiteSpace(label)
                && label.Equals(label.Trim(), StringComparison.Ordinal)
                && !label.Any(char.IsControl)
                && label.Length <= 50) == true)
        {
            return;
        }

        throw new InvalidDataException(
            "Pipeline change-request configuration is invalid.");
    }
}