// -----------------------------------------------------------------------
// <copyright file="CheckModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// 检查数据的Module.
/// </summary>
/// <typeparam name="T">返回数据类型.</typeparam>
[DependsOn<DotnetBuildModule>]
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(CompileModule<>))]
public abstract class CheckModule<T> : Module<T>;