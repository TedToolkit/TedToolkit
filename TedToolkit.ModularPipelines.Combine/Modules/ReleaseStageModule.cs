// -----------------------------------------------------------------------
// <copyright file="ReleaseStageModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Combine.Modules;

/// <summary>
/// Marks the public Combine release stage for trusted consumer dependencies.
/// </summary>
/// <typeparam name="T">The module result type.</typeparam>
public abstract class ReleaseStageModule<T> : Module<T>;