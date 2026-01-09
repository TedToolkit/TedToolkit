// -----------------------------------------------------------------------
// <copyright file="CleanModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// To clean module.
/// </summary>
/// <typeparam name="T">return type.</typeparam>
public abstract class CleanModule<T> : Module<T>;