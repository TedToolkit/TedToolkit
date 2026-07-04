// -----------------------------------------------------------------------
// <copyright file="CompileModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Base module for compilation steps.
/// </summary>
/// <typeparam name="T">The return type.</typeparam>
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareModule<>))]
public abstract class CompileModule<T> : Module<T>;