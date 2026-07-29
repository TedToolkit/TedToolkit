// -----------------------------------------------------------------------
// <copyright file="CommandRequest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents one structured, shell-free child-process request.
/// </summary>
/// <param name="FileName">The executable filename.</param>
/// <param name="Arguments">The structured argument tokens.</param>
/// <param name="WorkingDirectory">The explicit working directory.</param>
/// <param name="Timeout">The bounded command timeout.</param>
internal sealed record CommandRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout);