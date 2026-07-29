// -----------------------------------------------------------------------
// <copyright file="CleanStageModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Build.Modules;

/// <summary>
/// Marks a Build module that initializes or cleans validated local state.
/// </summary>
/// <typeparam name="T">The module result type.</typeparam>
public abstract class CleanStageModule<T> : Module<T>;