// -----------------------------------------------------------------------
// <copyright file="PrepareModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Modules;

namespace TedToolkit.ModularPipelines.Modules;

/// <summary>
/// 所有的准备Module，一般来说就是准备处理一些代码的内容，因此只在Windows上能够使用。
/// </summary>
/// <typeparam name="T">返回类型</typeparam>
[DependsOnAllModulesInheritingFrom(typeof(CleanModule<>))]
public abstract class PrepareModule<T> : Module<T>;