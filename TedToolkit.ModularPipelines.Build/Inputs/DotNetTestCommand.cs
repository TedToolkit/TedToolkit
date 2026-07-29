// -----------------------------------------------------------------------
// <copyright file="DotNetTestCommand.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.ModularPipelines.Build.Inputs;

/// <summary>
/// Represents the supported, structurally distinct .NET test command shapes.
/// </summary>
public enum DotNetTestCommand
{
    /// <summary>
    /// Uses <c>dotnet run</c> and passes Microsoft.Testing.Platform reporting arguments after <c>--</c>.
    /// </summary>
    MicrosoftTestingPlatformRun = 0,

    /// <summary>
    /// Uses the .NET 10 Microsoft.Testing.Platform form of <c>dotnet test</c>.
    /// </summary>
    MicrosoftTestingPlatformTest = 1,

    /// <summary>
    /// Uses the conventional VSTest logger form of <c>dotnet test</c>.
    /// </summary>
    VSTest = 2,
}