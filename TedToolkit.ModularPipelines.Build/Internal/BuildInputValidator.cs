// -----------------------------------------------------------------------
// <copyright file="BuildInputValidator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;

using NuGet.Versioning;

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Build.Internal;

/// <summary>
/// Validates the complete Build input, path, target, argument, and profile-write contract.
/// </summary>
internal static partial class BuildInputValidator
{
    private static readonly string[] OwnedArgumentNames =
    [
        "configuration",
        "c",
        "output",
        "o",
        "packageoutputpath",
        "packageversion",
        "version",
        "framework",
        "f",
        "runtime",
        "r",
        "self-contained",
        "selfcontained",
        "logger",
        "l",
        "results-directory",
        "resultsdirectory",
        "project",
        "include",
        "generatepackageonbuild",
    ];

    /// <summary>
    /// Validates all inputs before a command, delete, write, or callback can run.
    /// </summary>
    /// <param name="profile">The selected Build profile.</param>
    /// <param name="inputs">The explicit Build inputs.</param>
    /// <param name="options">The resolved Build options.</param>
    /// <exception cref="InvalidDataException">Any profile, path, name, argument, target, or write-matrix rule is violated.</exception>
    public static void Validate(
        BuildExecutionProfile profile,
        BuildInputs inputs,
        BuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Conventions is null
            || options.Resources is null
            || inputs.FormatTargets is null
            || inputs.BuildTargets is null
            || inputs.TestTargets is null
            || inputs.PackTargets is null
            || inputs.PublishTargets is null)
        {
            throw new InvalidDataException(
                "Build option and target collections cannot be null.");
        }

        if (!Enum.IsDefined(profile))
        {
            throw new InvalidDataException(
                $"Unknown Build execution profile '{profile}'.");
        }

        if (profile == BuildExecutionProfile.None)
        {
            return;
        }

        if (options.MaxDegreeOfParallelism <= 0)
        {
            throw new InvalidDataException(
                "Maximum degree of parallelism must be positive.");
        }

        var rootPath = PathSafety.EnsureExistingRoot(inputs.RootDirectory);
        _ = PathSafety.EnsureStrictDescendant(
            rootPath,
            inputs.ArtifactRootDirectory,
            mustExist: false);

        if (File.Exists(inputs.ArtifactRootDirectory.FullName))
        {
            throw new InvalidDataException(
                "The artifact root cannot be a regular file.");
        }

        ValidateText(inputs.SourceRevision, "source revision");
        ValidateConventions(options.Conventions);

        if (!Enum.IsDefined(options.ChangeDescriptionFailureMode))
        {
            throw new InvalidDataException(
                "The change-description failure mode is invalid.");
        }

        if (profile != BuildExecutionProfile.LocalBuild
            && (options.RunFormat || options.Resources.WriteEditorConfig))
        {
            throw new InvalidDataException(
                "Formatting and editor-config writes are LocalBuild-only.");
        }

        if (options.RunFormat && inputs.FormatTargets.Count == 0)
        {
            throw new InvalidDataException(
                "At least one format target is required when formatting is enabled.");
        }

        if (inputs.MsBuildVersion is null)
        {
            throw new InvalidDataException(
                "Every active Build profile requires version recovery configuration.");
        }

        ValidateMsBuildVersion(
            rootPath,
            inputs,
            profile);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in inputs.FormatTargets)
        {
            if (!Enum.IsDefined(target.Scope))
            {
                throw new InvalidDataException(
                    "A format target has an invalid path scope.");
            }

            ValidateTargetFile(rootPath, target.File);
            ValidateArguments(target.Arguments);
            ValidateTimeout(target.Timeout);
        }

        foreach (var target in inputs.BuildTargets)
        {
            ValidateNamedTarget(
                rootPath,
                target.Name,
                target.File,
                target.Configuration,
                target.Arguments,
                target.Timeout,
                names);
        }

        foreach (var target in inputs.TestTargets)
        {
            if (!Enum.IsDefined(target.Command))
            {
                throw new InvalidDataException(
                    $"Test target '{target.Name}' has an invalid command.");
            }

            ValidateNamedTarget(
                rootPath,
                target.Name,
                target.File,
                target.Configuration,
                target.CommandArguments,
                target.Timeout,
                names);
            ValidateArguments(target.RunnerArguments);

            if (target.Command == DotNetTestCommand.VSTest
                && target.RunnerArguments.Count != 0)
            {
                throw new InvalidDataException(
                    $"VSTest target '{target.Name}' cannot contain runner arguments.");
            }

            ValidateTestRunner(rootPath, target);
        }

        var packageVersions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var target in inputs.PackTargets)
        {
            ValidateNamedTarget(
                rootPath,
                target.Name,
                target.File,
                target.Configuration,
                target.Arguments,
                target.Timeout,
                names);
            packageVersions.Add(
                NormalizePackageVersion(target.PackageVersion));
        }

        if (packageVersions.Count > 1)
        {
            throw new InvalidDataException(
                "All pack targets in one run must use one package version.");
        }

        if (profile == BuildExecutionProfile.Pack
            && inputs.PackTargets.Count == 0)
        {
            throw new InvalidDataException(
                "The Pack profile requires at least one pack target.");
        }

        if (profile == BuildExecutionProfile.Publish
            && inputs.PublishTargets.Count == 0)
        {
            throw new InvalidDataException(
                "The Publish profile requires at least one publish target.");
        }

        if (profile is not (
            BuildExecutionProfile.Pack
            or BuildExecutionProfile.Publish)
            && inputs.PackTargets.Count != 0)
        {
            throw new InvalidDataException(
                "Pack targets are valid only for Pack and Publish profiles.");
        }

        if (profile != BuildExecutionProfile.Publish
            && inputs.PublishTargets.Count != 0)
        {
            throw new InvalidDataException(
                "Publish targets are valid only for the Publish profile.");
        }

        var artifactNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var target in inputs.PublishTargets)
        {
            ValidateNamedTarget(
                rootPath,
                target.Name,
                target.File,
                target.Configuration,
                target.Arguments,
                target.Timeout,
                names);

            if (!ArtifactNameRegex().IsMatch(target.ArtifactName)
                || target.ArtifactName is "." or ".."
                || !artifactNames.Add(target.ArtifactName))
            {
                throw new InvalidDataException(
                    $"Publish artifact name '{target.ArtifactName}' is invalid or duplicated.");
            }

            if (target.RuntimeIdentifier is not null
                && target.SelfContained is null)
            {
                throw new InvalidDataException(
                    $"Publish target '{target.Name}' must explicitly choose SelfContained when a runtime identifier is supplied.");
            }
        }

        var archiveNames = inputs.PublishTargets
            .Select(GetArchiveName)
            .ToArray();

        if (archiveNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            == archiveNames.Length)
        {
            return;
        }

        throw new InvalidDataException(
            "Publish target metadata produces colliding archive filenames.");
    }

    private static void ValidateTestRunner(
        string rootPath,
        TestTarget target)
    {
        if (target.Command == DotNetTestCommand.MicrosoftTestingPlatformRun)
        {
            return;
        }

        var globalPath = Path.Combine(rootPath, "global.json");
        string? runner = null;

        if (File.Exists(globalPath))
        {
            using var document = JsonDocument.Parse(
                File.ReadAllBytes(globalPath));

            if (document.RootElement.TryGetProperty("test", out var test)
                && test.TryGetProperty("runner", out var runnerElement))
            {
                runner = runnerElement.GetString();
            }
        }

        if (target.Command == DotNetTestCommand.MicrosoftTestingPlatformTest
            && !string.Equals(
                runner,
                "Microsoft.Testing.Platform",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Test target '{target.Name}' requires a root-contained Microsoft.Testing.Platform global.json selection.");
        }

        if (target.Command != DotNetTestCommand.VSTest
            || runner is null
            || string.Equals(
                runner,
                "VSTest",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidDataException(
            $"Test target '{target.Name}' conflicts with the root-contained test runner selection.");
    }

    private static string GetArchiveName(DotnetPublishTarget target)
    {
        var suffix = string.Join(
            "-",
            new[] { target.Framework, target.RuntimeIdentifier, }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrEmpty(suffix)
            ? $"{target.ArtifactName}.zip"
            : $"{target.ArtifactName}-{suffix}.zip";
    }

    private static void ValidateMsBuildVersion(
        string rootPath,
        BuildInputs inputs,
        BuildExecutionProfile profile)
    {
        var version = inputs.MsBuildVersion!;
        var targetPath = PathSafety.EnsureStrictDescendant(
            rootPath,
            version.TargetFile,
            mustExist: true);
        var recoveryPath = PathSafety.EnsureStrictDescendant(
            rootPath,
            version.RecoveryFile,
            mustExist: false);
        var artifactPath = Path.GetFullPath(
            inputs.ArtifactRootDirectory.FullName);

        if (!File.Exists(targetPath)
            || string.Equals(
                targetPath,
                recoveryPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)
            || PathSafety.IsWithin(artifactPath, recoveryPath))
        {
            throw new InvalidDataException(
                "The version target/recovery paths are invalid.");
        }

        try
        {
            _ = System.Xml.XmlConvert.VerifyNCName(version.PropertyName);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or System.Xml.XmlException)
        {
            throw new InvalidDataException(
                "The MSBuild version property name is invalid.",
                exception);
        }

        if (profile is BuildExecutionProfile.LocalBuild
            or BuildExecutionProfile.Validate
            or BuildExecutionProfile.Message)
        {
            if (version.TargetVersion is not null)
            {
                throw new InvalidDataException(
                    "Only Pack and Publish may begin a new version transaction.");
            }

            return;
        }

        if (version.TargetVersion is null)
        {
            return;
        }

        _ = Versioning.DailyReleaseVersionPolicy.Parse(version.TargetVersion);

        if (inputs.PackTargets.All(target => string.Equals(
                NormalizePackageVersion(target.PackageVersion),
                version.TargetVersion,
                StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidDataException(
            "Every pack target version must match the temporary MSBuild version.");
    }

    private static void ValidateNamedTarget(
        string rootPath,
        string name,
        FileInfo file,
        string configuration,
        IReadOnlyList<string> arguments,
        in TimeSpan timeout,
        HashSet<string> names)
    {
        if (!ArtifactNameRegex().IsMatch(name)
            || name is "." or "..")
        {
            throw new InvalidDataException(
                $"Target name '{name}' is invalid.");
        }

        if (!names.Add(name))
        {
            throw new InvalidDataException(
                $"Target name '{name}' is duplicated.");
        }

        ValidateTargetFile(rootPath, file);
        ValidateText(configuration, "configuration");
        ValidateArguments(arguments);
        ValidateTimeout(timeout);
    }

    private static void ValidateTargetFile(
        string rootPath,
        FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = PathSafety.EnsureStrictDescendant(
            rootPath,
            file,
            mustExist: true);

        if (File.Exists(path))
        {
            return;
        }

        throw new InvalidDataException(
            $"Target path '{path}' must be a regular file.");
    }

    private static void ValidateArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument)
                || argument.Any(char.IsControl)
                || argument[0] == '@'
                || argument == "--")
            {
                throw new InvalidDataException(
                    "Command arguments must be non-empty structured tokens.");
            }

            var normalized = argument.TrimStart('-', '/');
            var separator = normalized.IndexOfAny(['=', ':',]);
            var name = separator < 0
                ? normalized
                : normalized[..separator];

            if (OwnedArgumentNames.Contains(
                    name,
                    StringComparer.OrdinalIgnoreCase)
                || (normalized.StartsWith("p:", StringComparison.OrdinalIgnoreCase)
                && OwnedArgumentNames.Contains(
                    normalized[2..].Split('=')[0],
                    StringComparer.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    $"Command argument '{argument}' overrides a package-owned switch.");
            }
        }
    }

    private static void ValidateTimeout(in TimeSpan timeout)
    {
        if (timeout >= TimeSpan.FromSeconds(1)
            && timeout <= TimeSpan.FromHours(24))
        {
            return;
        }

        throw new InvalidDataException(
            "Target timeout must be between one second and 24 hours.");
    }

    private static void ValidateConventions(
        PipelineConventionOptions conventions)
    {
        ArgumentNullException.ThrowIfNull(conventions);
        ValidateGitName(conventions.MainBranch, "main branch");
        ValidateGitName(conventions.DevelopmentBranch, "development branch");
        ValidateGitName(conventions.GitRemoteName, "Git remote");

        var segments = new string[]
        {
            conventions.Layout.PropsDirectoryName,
            conventions.Layout.OutputDirectoryName,
            conventions.Layout.ExternalsDirectoryName,
            conventions.Layout.NuGetDirectoryName,
            conventions.Layout.TestDirectoryName,
            conventions.Layout.PublishDirectoryName,
            conventions.Layout.ManifestFileName,
        };

        if (segments.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != segments.Length)
        {
            throw new InvalidDataException(
                "Pipeline layout names must be distinct.");
        }

        var invalidSegment = segments.FirstOrDefault(segment =>
            string.IsNullOrWhiteSpace(segment)
            || !PortableSegmentRegex().IsMatch(segment)
            || segment is "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.'));

        if (invalidSegment is not null)
        {
            throw new InvalidDataException(
                $"Pipeline layout segment '{invalidSegment}' is invalid.");
        }

        if (conventions.Layout.ManifestFileName.EndsWith(
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidDataException(
            "The manifest filename must end in .json.");
    }

    private static void ValidateGitName(string value, string fieldName)
    {
        ValidateText(value, fieldName);

        if (!value.Contains("..", StringComparison.Ordinal)
            && !value.StartsWith('.')
            && !value.EndsWith('.')
            && !value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            && value.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\',]) < 0)
        {
            return;
        }

        throw new InvalidDataException(
            $"The configured {fieldName} is not a valid Git name.");
    }

    private static void ValidateText(string value, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsWhiteSpace)
            && !value.Any(char.IsControl))
        {
            return;
        }

        throw new InvalidDataException(
            $"The configured {fieldName} is invalid.");
    }

    private static string NormalizePackageVersion(string value)
    {
        if (!NuGetVersion.TryParse(value, out var parsed))
        {
            throw new InvalidDataException(
                $"Package version '{value}' is invalid.");
        }

        return parsed.ToNormalizedString();
    }

    [GeneratedRegex(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactNameRegex();

    [GeneratedRegex(
        "^[^/\\\\:*?\"<>|\\x00-\\x1F]{1,128}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PortableSegmentRegex();
}