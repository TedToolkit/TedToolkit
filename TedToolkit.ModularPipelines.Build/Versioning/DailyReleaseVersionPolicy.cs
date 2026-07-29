// -----------------------------------------------------------------------
// <copyright file="DailyReleaseVersionPolicy.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;

namespace TedToolkit.ModularPipelines.Build.Versioning;

/// <summary>
/// Represents the pure policy that allocates assembly-compatible daily calendar versions.
/// </summary>
public sealed partial class DailyReleaseVersionPolicy
{
    private readonly int _maximumCounter = 65534;

    /// <summary>
    /// Resolves a buildable calendar version from explicit caller-selected facts.
    /// </summary>
    /// <param name="request">The date, revision, consumed history, and active-reservation state.</param>
    /// <returns>A new version, a recovery-required result, or an exhausted result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">A consumed version is malformed, non-normalized, duplicated, or conflicting.</exception>
    public ReleaseVersionResolution Resolve(DailyReleaseVersionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRevision(request.SourceRevision, nameof(request.SourceRevision));

        if (request.Date.Year < 1000)
        {
            throw new InvalidDataException(
                "The release date year must contain four digits.");
        }

        if (request.HasActivePublicationReservation)
        {
            return new() { Kind = ReleaseVersionResolutionKind.PublicationRecoveryRequired, };
        }

        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var highestCounter = -1;

        foreach (var record in request.ConsumedVersions)
        {
            ArgumentNullException.ThrowIfNull(record);
            ValidateRevision(record.SourceRevision, nameof(record.SourceRevision));

            if (!Enum.IsDefined(record.Disposition))
            {
                throw new InvalidDataException(
                    "A consumed release disposition is invalid.");
            }

            var parsed = Parse(record.Version);

            if (!seenVersions.Add(parsed.Normalized))
            {
                throw new InvalidDataException(
                    $"Duplicate consumed release version '{parsed.Normalized}'.");
            }

            if (parsed.Date == request.Date)
            {
                highestCounter = Math.Max(highestCounter, parsed.Counter);
            }
        }

        var nextCounter = highestCounter + 1;

        if (nextCounter > _maximumCounter)
        {
            return new() { Kind = ReleaseVersionResolutionKind.Exhausted, };
        }

        var version = nextCounter == 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{request.Date.Year}.{request.Date.Month}.{request.Date.Day}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{request.Date.Year}.{request.Date.Month}.{request.Date.Day}.{nextCounter}");
        return new()
        {
            Kind = ReleaseVersionResolutionKind.NewVersion,
            Version = version,
            Counter = nextCounter,
        };
    }

    /// <summary>
    /// Parses and validates one normalized assembly-compatible calendar version.
    /// </summary>
    /// <param name="version">The normalized calendar version.</param>
    /// <returns>The parsed date, counter, and normalized value.</returns>
    /// <exception cref="InvalidDataException"><paramref name="version"/> is malformed or non-normalized.</exception>
    internal static ParsedCalendarVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException("A consumed release version is required.");
        }

        var match = CalendarVersionRegex().Match(version);

        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Invalid calendar release version '{version}'.");
        }

        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var counter = match.Groups["counter"].Success
            ? int.Parse(match.Groups["counter"].Value, CultureInfo.InvariantCulture)
            : 0;

        if (counter > 65534)
        {
            throw new InvalidDataException(
                $"Calendar release counter '{counter}' exceeds 65534.");
        }

        DateOnly date;

        try
        {
            date = new(year, month, day);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                $"Invalid calendar release version '{version}'.",
                exception);
        }

        var normalized = counter == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{year}.{month}.{day}")
            : string.Create(CultureInfo.InvariantCulture, $"{year}.{month}.{day}.{counter}");

        if (!string.Equals(version, normalized, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Calendar release version '{version}' is not normalized.");
        }

        return new(date, counter, normalized);
    }

    private static void ValidateRevision(string revision, string parameterName)
    {
        if (!string.IsNullOrWhiteSpace(revision)
            && !revision.Any(char.IsWhiteSpace)
            && !revision.Any(char.IsControl))
        {
            return;
        }

        throw new ArgumentException(
            "A non-empty control-free source revision is required.",
            parameterName);
    }

    [GeneratedRegex(
        "^(?<year>[1-9][0-9]{3,})\\.(?<month>[1-9]|1[0-2])\\.(?<day>[1-9]|[12][0-9]|3[01])(?:\\.(?<counter>[1-9][0-9]{0,4}))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CalendarVersionRegex();

    /// <summary>
    /// Represents a parsed normalized calendar version.
    /// </summary>
    /// <param name="Date">The calendar date.</param>
    /// <param name="Counter">The assembly-compatible counter.</param>
    /// <param name="Normalized">The normalized package version.</param>
    internal sealed record ParsedCalendarVersion(
        DateOnly Date,
        int Counter,
        string Normalized);
}