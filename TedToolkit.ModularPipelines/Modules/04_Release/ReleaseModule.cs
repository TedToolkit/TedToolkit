// -----------------------------------------------------------------------
// <copyright file="ReleaseModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// For the release stage.
/// </summary>
/// <typeparam name="T">return type.</typeparam>
[DependsOn<DotnetBuildModule>]
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(CompileModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(CompileCheckModule<>))]
public abstract class ReleaseModule<T> : Module<T>;