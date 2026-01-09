// -----------------------------------------------------------------------
// <copyright file="CompileCheckModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Compile module.
/// </summary>
/// <typeparam name="T">return type.</typeparam>
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareModule<>))]
public abstract class CompileCheckModule<T> : Module<T>;