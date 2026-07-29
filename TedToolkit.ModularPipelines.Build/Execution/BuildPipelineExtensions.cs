// -----------------------------------------------------------------------
// <copyright file="BuildPipelineExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Descriptions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Build.Internal;
using TedToolkit.ModularPipelines.Build.Modules;
using TedToolkit.ModularPipelines.Build.Resources;

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Provides Build-only ModularPipelines composition over explicit validated inputs.
/// </summary>
public static class BuildPipelineExtensions
{
    /// <summary>
    /// Adds the fixed local Build graph for one execution profile and returns the same builder.
    /// </summary>
    /// <param name="builder">The ModularPipelines builder to configure.</param>
    /// <param name="profile">The local Build graph to register.</param>
    /// <param name="inputs">Explicit roots, revision, recovery state, and targets.</param>
    /// <param name="options">Optional conventions and opt-in behavior.</param>
    /// <returns>The same <paramref name="builder"/> instance.</returns>
    public static PipelineBuilder AddBuildPipeline(
        this PipelineBuilder builder,
        BuildExecutionProfile profile,
        BuildInputs inputs,
        BuildOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (profile == BuildExecutionProfile.None)
        {
            return builder;
        }

        options ??= new();
        BuildInputValidator.Validate(profile, inputs, options);
        var registration = new BuildPipelineRegistration(
            profile,
            inputs,
            options);
        builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(registration);
            services.AddSingleton<ICommandExecutor, ProcessCommandExecutor>();
            services.AddSingleton<
                IPipelineResourceComposer,
                PipelineResourceComposer>();
            services.AddSingleton<
                IPipelineArtifactManifestReader,
                PipelineArtifactManifestStore>();
            services.AddSingleton<
                IPipelineArtifactManifestWriter,
                PipelineArtifactManifestStore>();
            services.AddSingleton(provider =>
                new BuildExecutionEngine(
                    registration.Profile,
                    registration.Inputs,
                    registration.Options,
                    provider.GetRequiredService<ICommandExecutor>(),
                    provider.GetRequiredService<IPipelineResourceComposer>(),
                    provider.GetRequiredService<
                        IPipelineArtifactManifestWriter>(),
                    provider.GetService<IChangeDescriptionGenerator>()));
            services.AddSingleton<BuildPipelineState>();
        });
        builder.AddModule<CleanOutputModule>();
        builder.AddModule<UpdateEditorConfigModule>();
        builder.AddModule<FormatCodeModule>();
        builder.AddModule<DotnetBuildModule>();
        builder.AddModule<TestModule>();
        builder.AddModule<AssertBuildTestModule>();
        builder.AddModule<GenerateCommitMessageModule>();
        builder.AddModule<DotnetPackModule>();
        builder.AddModule<DotnetPublishModule>();
        builder.AddModule<CollectPublishArtifactsModule>();
        builder.AddModule<WriteArtifactManifestModule>();
        return builder;
    }
}