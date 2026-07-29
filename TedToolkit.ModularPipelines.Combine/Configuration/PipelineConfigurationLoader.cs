// -----------------------------------------------------------------------
// <copyright file="PipelineConfigurationLoader.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Reflection;

using Microsoft.Extensions.Configuration;

using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Models;

namespace TedToolkit.ModularPipelines.Combine.Configuration;

/// <summary>
/// Loads only strict non-secret package configuration from a composed source.
/// </summary>
public static class PipelineConfigurationLoader
{
    /// <summary>
    /// Loads the <c>TedToolkit:Build</c> and <c>TedToolkit:Pipeline</c>
    /// sections without reading files, environment variables, or secrets.
    /// </summary>
    /// <param name="configuration">The caller-composed configuration root.</param>
    /// <returns>The strictly bound package options.</returns>
    /// <exception cref="InvalidDataException">
    /// A package-owned path is unknown, secret-shaped, or malformed.
    /// </exception>
    public static PipelineConfiguration Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var root = configuration.GetSection("TedToolkit");
        var invalidChild = root.GetChildren().FirstOrDefault(child =>
            !child.Key.Equals(
                "Build",
                StringComparison.OrdinalIgnoreCase)
            && !child.Key.Equals(
                "Pipeline",
                StringComparison.OrdinalIgnoreCase));

        if (invalidChild is not null)
        {
            throw InvalidPath(invalidChild.Path);
        }

        var build = Bind<BuildOptions>(root.GetSection("Build"));
        var pipeline = Bind<CombineOptions>(root.GetSection("Pipeline"));
        return new(build, pipeline);
    }

    private static T Bind<T>(IConfigurationSection section)
        where T : new()
    {
        return (T)BindObject(typeof(T), section);
    }

    private static object BindObject(
        Type objectType,
        IConfigurationSection section)
    {
        var instance = Activator.CreateInstance(objectType)
            ?? throw new InvalidOperationException(
                $"Configuration type {objectType.Name} cannot be constructed.");
        var properties = objectType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.SetMethod is not null
                && property.PropertyType
                != typeof(IPackagePublicationCheckpoint)
                && property.PropertyType
                != typeof(IReadOnlyList<CommitSummary>))
            .ToDictionary(
                property => property.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var child in section.GetChildren())
        {
            RejectSecretProperty(child);

            if (!properties.TryGetValue(child.Key, out var property))
            {
                throw InvalidPath(child.Path);
            }

            var value = BindValue(property.PropertyType, child);
            property.SetValue(instance, value);
        }

        return instance;
    }

    private static object? BindValue(
        Type targetType,
        IConfigurationSection section)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);

        if (nullableType is not null)
        {
            if (section.Value is null
                && !section.GetChildren().Any())
            {
                return null;
            }

            return BindValue(nullableType, section);
        }

        if (targetType == typeof(string))
        {
            return section.Value;
        }

        if (targetType == typeof(bool))
        {
            return Parse(
                section,
                value => bool.Parse(value));
        }

        if (targetType == typeof(int))
        {
            return Parse(
                section,
                value => int.Parse(value, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(long))
        {
            return Parse(
                section,
                value => long.Parse(value, CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(TimeSpan))
        {
            return Parse(
                section,
                value => TimeSpan.Parse(
                    value,
                    CultureInfo.InvariantCulture));
        }

        if (targetType == typeof(Uri))
        {
            return Parse(
                section,
                value => new Uri(value, UriKind.Absolute));
        }

        if (targetType.IsEnum)
        {
            return Parse(
                section,
                value => Enum.Parse(
                    targetType,
                    value,
                    ignoreCase: true));
        }

        if (TryGetListElementType(targetType, out var elementType))
        {
            var children = section.GetChildren().ToArray();

            if (elementType != typeof(string)
                || children.Any(child =>
                    !int.TryParse(
                        child.Key,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out _)))
            {
                throw InvalidPath(section.Path);
            }

            return children
                .OrderBy(child => int.Parse(
                    child.Key,
                    CultureInfo.InvariantCulture))
                .Select(child => child.Value
                    ?? throw InvalidPath(child.Path))
                .ToArray();
        }

        if (targetType.IsClass)
        {
            return BindObject(targetType, section);
        }

        throw InvalidPath(section.Path);
    }

    private static object Parse(
        IConfigurationSection section,
        Func<string, object> parser)
    {
        try
        {
            return parser(section.Value
                ?? throw InvalidPath(section.Path));
        }
        catch (Exception exception) when (
            exception is FormatException
            or OverflowException
            or UriFormatException
            or ArgumentException)
        {
            throw new InvalidDataException(
                $"Configuration path '{section.Path}' is invalid.",
                exception);
        }
    }

    private static bool TryGetListElementType(
        Type type,
        out Type? elementType)
    {
        var listInterface = type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(candidate =>
                    candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition()
                    == typeof(IReadOnlyList<>));
        elementType = listInterface?.GetGenericArguments()[0];
        return elementType is not null;
    }

    private static void RejectSecretProperty(IConfigurationSection section)
    {
        if (section.Value is null
            || string.IsNullOrEmpty(section.Value))
        {
            return;
        }

        if (!section.Key.Equals("Token", StringComparison.OrdinalIgnoreCase)
            && !section.Key.Equals("ApiKey", StringComparison.OrdinalIgnoreCase)
            && !section.Key.Equals("Password", StringComparison.OrdinalIgnoreCase)
            && !section.Key.Equals("Secret", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw InvalidPath(section.Path);
    }

    private static InvalidDataException InvalidPath(string path)
    {
        return new(
            $"Configuration path '{path}' is not allowed.");
    }
}