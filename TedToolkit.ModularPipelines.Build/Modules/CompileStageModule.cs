// -----------------------------------------------------------------------
// <copyright file="CompileStageModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Marks a Build module that compiles validated targets.
/// </summary>
/// <typeparam name="T">The module result type.</typeparam>
[DependsOnAllModulesInheritingFrom(typeof(CleanStageModule<>))]
[DependsOnAllModulesInheritingFrom(typeof(PrepareStageModule<>))]
public abstract class CompileStageModule<T> : Module<T>;