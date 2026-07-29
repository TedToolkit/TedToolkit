// -----------------------------------------------------------------------
// <copyright file="StandardPipelineExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Artifacts;
using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Events;
using TedToolkit.ModularPipelines.Combine.Internal;
using TedToolkit.ModularPipelines.Combine.Modules;

namespace TedToolkit.ModularPipelines.Combine.Execution;

/// <summary>
/// Provides the fixed provider-neutral Combine composition entry point.
/// </summary>
public static class StandardPipelineExtensions
{
    /// <summary>
    /// Adds one validated fixed pipeline and returns the same builder.
    /// </summary>
    /// <param name="builder">The ModularPipelines builder.</param>
    /// <param name="rootDirectory">The explicit consumer root.</param>
    /// <param name="combineOptions">The authoritative Combine options.</param>
    /// <param name="buildInputs">Build inputs for RunBuild.</param>
    /// <param name="buildOptions">Optional Build conventions and behavior.</param>
    /// <param name="moduleRegistrations">Trusted profile-scoped extensions.</param>
    /// <returns>The same <paramref name="builder"/> instance.</returns>
    public static PipelineBuilder AddStandardPipeline(
        this PipelineBuilder builder,
        DirectoryInfo rootDirectory,
        CombineOptions combineOptions,
        BuildInputs? buildInputs = null,
        BuildOptions? buildOptions = null,
        IEnumerable<PipelineModuleRegistration>? moduleRegistrations = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        buildOptions ??= new();
        var registration = CombineOptionsValidator.Validate(
            rootDirectory,
            combineOptions,
            buildInputs,
            buildOptions,
            moduleRegistrations);

        if (registration.Profile == PipelineProfile.None)
        {
            return builder;
        }

        if (combineOptions.InputMode == PipelineInputMode.RunBuild)
        {
            builder.AddBuildPipeline(
                MapProfile(registration.Profile),
                buildInputs!,
                buildOptions);
        }

        foreach (var extension in registration.ModuleRegistrations.Where(
                     extension =>
                         extension.Profiles.Contains(registration.Profile)))
        {
            extension.Configure(builder);
        }

        builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(registration);
            services.TryAddSingleton<
                IPipelineArtifactManifestReader,
                PipelineArtifactManifestStore>();
            services.TryAddSingleton<
                IPipelineSecretResolver,
                EnvironmentPipelineSecretResolver>();
            services.TryAddSingleton<
                ICombineCommandExecutor,
                CombineCommandExecutor>();
            services.TryAddSingleton<
                IProviderHttpTransport,
                ProviderHttpTransport>();
            services.TryAddSingleton<
                IRepositoryProviderFactory,
                RepositoryProviderFactory>();
            services.TryAddSingleton<IReleaseFinalizer, ReleaseFinalizer>();
            services.AddSingleton<NuGetPublicationService>();
            services.AddSingleton<CombineExecutionEngine>();
        });

        if (combineOptions.InputMode == PipelineInputMode.RunBuild)
        {
            builder.AddModule<RunBuildCombineModule>();
        }
        else
        {
            builder.AddModule<ConsumeArtifactsCombineModule>();
        }

        return builder;
    }

    private static BuildExecutionProfile MapProfile(PipelineProfile profile)
    {
        return profile switch
        {
            PipelineProfile.LocalBuild => BuildExecutionProfile.LocalBuild,
            PipelineProfile.Validate => BuildExecutionProfile.Validate,
            PipelineProfile.Pack => BuildExecutionProfile.Pack,
            PipelineProfile.Publish => BuildExecutionProfile.Publish,
            PipelineProfile.Message => BuildExecutionProfile.Message,
            _ => throw new InvalidOperationException(
                "None does not map to a Build profile."),
        };
    }
}