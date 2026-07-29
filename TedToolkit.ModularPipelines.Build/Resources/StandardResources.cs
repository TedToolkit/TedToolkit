// -----------------------------------------------------------------------
// <copyright file="StandardResources.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Provides the versioned embedded editor, commit, and change-request baselines.
/// </summary>
public static class StandardResources
{
    /// <summary>
    /// Identifies the semantic version of all embedded baseline bytes.
    /// </summary>
    public const int ResourceVersion = 1;

    /// <summary>
    /// Gets the normalized embedded editor-configuration baseline.
    /// </summary>
    public static string EditorConfigBase { get; } = Read("EditorConfig.base");

    /// <summary>
    /// Gets the normalized embedded commit-message instruction baseline.
    /// </summary>
    public static string CommitMessagePromptBase { get; } = Read("CommitMessage.base.md");

    /// <summary>
    /// Gets the normalized embedded change-request instruction baseline.
    /// </summary>
    public static string ChangeRequestPromptBase { get; } = Read("ChangeRequest.base.md");

    private static string Read(string suffix)
    {
        var assembly = typeof(StandardResources).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true);
        return $"{reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n')}\n";
    }
}