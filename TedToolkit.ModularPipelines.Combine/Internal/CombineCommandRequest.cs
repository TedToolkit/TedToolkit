// -----------------------------------------------------------------------
// <copyright file="CombineCommandRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Describes one bounded structured child-process invocation.
/// </summary>
/// <param name="FileName">The executable name.</param>
/// <param name="Arguments">The structured argument list.</param>
/// <param name="WorkingDirectory">The explicit working directory.</param>
/// <param name="Timeout">The execution timeout.</param>
internal sealed record CombineCommandRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout);