// -----------------------------------------------------------------------
// <copyright file="PipelineResourceDocument.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Resources;

/// <summary>
/// Represents one normalized, versioned, content-addressed resource document.
/// </summary>
/// <param name="Content">The LF-normalized text with exactly one final newline.</param>
/// <param name="ResourceVersion">The embedded baseline semantic version.</param>
/// <param name="Sha256">The lowercase SHA-256 digest of UTF-8 bytes.</param>
/// <param name="Source">The composition precedence path.</param>
public sealed record PipelineResourceDocument(
    string Content,
    int ResourceVersion,
    string Sha256,
    PipelineResourceSource Source);