// -----------------------------------------------------------------------
// <copyright file="StandardProfileResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Combine.Configuration;

namespace TedToolkit.ModularPipelines.Combine.Internal;

/// <summary>
/// Resolves the exact neutral Standard or Manual profile.
/// </summary>
internal static class StandardProfileResolver
{
    /// <summary>
    /// Resolves and validates the execution context and profile.
    /// </summary>
    /// <param name="options">The authoritative Combine options.</param>
    /// <param name="conventions">The shared branch conventions.</param>
    /// <returns>The resolved profile and context.</returns>
    /// <exception cref="InvalidDataException">
    /// The policy, manual profile, or execution context is invalid.
    /// </exception>
    public static ResolvedExecution Resolve(
        CombineOptions options,
        PipelineConventionOptions conventions)
    {
        ValidateEnum(options.ExecutionPolicy, "ExecutionPolicy");

        if (options.ExecutionPolicy == PipelineExecutionPolicy.Manual)
        {
            if (options.ManualProfile is null)
            {
                throw new InvalidDataException(
                    "Pipeline:ManualProfile is required for Manual policy.");
            }

            ValidateEnum(options.ManualProfile.Value, "ManualProfile");
            return new(
                options.ManualProfile.Value,
                options.ExecutionContext);
        }

        if (options.ManualProfile is not null)
        {
            throw new InvalidDataException(
                "Pipeline:ManualProfile is forbidden for Standard policy.");
        }

        var context = options.ExecutionContext ?? ReadEnvironmentContext();
        ValidateContext(context);
        return new(ResolveStandard(context, conventions), context);
    }

    private static PipelineProfile ResolveStandard(
        PipelineExecutionContext context,
        PipelineConventionOptions conventions)
    {
        if (!context.IsCi
            && context.Trigger == PipelineTriggerKind.Local)
        {
            return PipelineProfile.LocalBuild;
        }

        if (context.Trigger == PipelineTriggerKind.Push
            && context.Branch is null)
        {
            return PipelineProfile.None;
        }

        if (context.Trigger == PipelineTriggerKind.Push
            && string.Equals(
                context.Branch,
                conventions.MainBranch,
                StringComparison.Ordinal))
        {
            return PipelineProfile.Publish;
        }

        if (context.Trigger == PipelineTriggerKind.Push
            && !string.IsNullOrWhiteSpace(context.Branch))
        {
            return PipelineProfile.Message;
        }

        if (context.Trigger is PipelineTriggerKind.Manual
                or PipelineTriggerKind.ReusableCall
            && string.Equals(
                context.Branch,
                conventions.MainBranch,
                StringComparison.Ordinal))
        {
            return PipelineProfile.Publish;
        }

        return PipelineProfile.None;
    }

    private static PipelineExecutionContext ReadEnvironmentContext()
    {
        var isGitHub = IsTrue(Environment.GetEnvironmentVariable(
            "GITHUB_ACTIONS"));
        var isGitLab = IsTrue(Environment.GetEnvironmentVariable(
            "GITLAB_CI"));

        if (isGitHub && isGitLab)
        {
            throw new InvalidDataException(
                "Both GitHub Actions and GitLab CI contexts are active.");
        }

        if (isGitHub)
        {
            var eventName = Environment.GetEnvironmentVariable(
                "GITHUB_EVENT_NAME");
            var refType = Environment.GetEnvironmentVariable(
                "GITHUB_REF_TYPE");
            var trigger = eventName switch
            {
                "push" => PipelineTriggerKind.Push,
                "workflow_dispatch" => PipelineTriggerKind.Manual,
                "workflow_call" => PipelineTriggerKind.ReusableCall,
                _ => PipelineTriggerKind.Other,
            };
            return new()
            {
                Trigger = trigger,
                IsCi = true,
                Branch = string.Equals(
                    refType,
                    "branch",
                    StringComparison.Ordinal)
                    ? Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
                    : null,
                SourceRevision = NormalizeRevision(
                    Environment.GetEnvironmentVariable("GITHUB_SHA")),
                BeforeRevision = NormalizeRevision(
                    GitHubActionsEnvironment.ReadBeforeRevision()),
                Actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR"),
                RunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
                RunUri = TryUri(BuildGitHubRunUri()),
                RepositoryUri = TryUri(BuildGitHubRepositoryUri()),
            };
        }

        if (isGitLab)
        {
            var source = Environment.GetEnvironmentVariable(
                "CI_PIPELINE_SOURCE");
            var trigger = source switch
            {
                "push" => PipelineTriggerKind.Push,
                "web" => PipelineTriggerKind.Manual,
                "api" or "pipeline" or "parent_pipeline"
                    => PipelineTriggerKind.ReusableCall,
                _ => PipelineTriggerKind.Other,
            };
            var isTag = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("CI_COMMIT_TAG"));
            return new()
            {
                Trigger = trigger,
                IsCi = true,
                Branch = isTag
                    ? null
                    : Environment.GetEnvironmentVariable(
                        "CI_COMMIT_REF_NAME"),
                SourceRevision = NormalizeRevision(
                    Environment.GetEnvironmentVariable("CI_COMMIT_SHA")),
                BeforeRevision = NormalizeRevision(
                    Environment.GetEnvironmentVariable(
                        "CI_COMMIT_BEFORE_SHA")),
                Actor = Environment.GetEnvironmentVariable(
                    "GITLAB_USER_LOGIN"),
                RunId = Environment.GetEnvironmentVariable(
                    "CI_PIPELINE_ID"),
                RunUri = TryUri(Environment.GetEnvironmentVariable(
                    "CI_PIPELINE_URL")),
                RepositoryUri = TryUri(Environment.GetEnvironmentVariable(
                    "CI_PROJECT_URL")),
            };
        }

        return new()
        {
            Trigger = PipelineTriggerKind.Local,
            IsCi = false,
        };
    }

    private static void ValidateContext(PipelineExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateEnum(context.Trigger, "ExecutionContext:Trigger");

        if ((context.Trigger == PipelineTriggerKind.Local && context.IsCi)
            || (context.Trigger != PipelineTriggerKind.Local && !context.IsCi))
        {
            throw new InvalidDataException(
                "Pipeline:ExecutionContext trigger and CI state are inconsistent.");
        }

        ValidateRevision(context.SourceRevision, "SourceRevision");
        ValidateRevision(context.BeforeRevision, "BeforeRevision");
        ValidateUri(context.RunUri, "RunUri");
        ValidateUri(context.RepositoryUri, "RepositoryUri");
        ValidateUri(context.CompareUri, "CompareUri");

        if (context.Branch is null
            || (!string.IsNullOrWhiteSpace(context.Branch)
                && !context.Branch.Any(char.IsControl)))
        {
            return;
        }

        throw new InvalidDataException(
            "Pipeline:ExecutionContext:Branch is invalid.");
    }

    private static void ValidateRevision(string? value, string name)
    {
        if (value is null
            || (value.Length is (40 or 64)
                && value.All(Uri.IsHexDigit)))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:ExecutionContext:{name} is invalid.");
    }

    private static void ValidateUri(Uri? value, string name)
    {
        if (value is null
            || (value.IsAbsoluteUri
                && value.Scheme is ("https" or "http")
                && string.IsNullOrEmpty(value.UserInfo)
                && string.IsNullOrEmpty(value.Query)
                && string.IsNullOrEmpty(value.Fragment)))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:ExecutionContext:{name} is invalid.");
    }

    private static string? NormalizeRevision(string? value)
    {
        return value?.All(character => character == '0') == true
            ? null
            : value;
    }

    private static bool IsTrue(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static Uri? TryUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static string? BuildGitHubRunUri()
    {
        var server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
        var repository = Environment.GetEnvironmentVariable(
            "GITHUB_REPOSITORY");
        var run = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
        return server is null || repository is null || run is null
            ? null
            : $"{server.TrimEnd('/')}/{repository}/actions/runs/{run}";
    }

    private static string? BuildGitHubRepositoryUri()
    {
        var server = Environment.GetEnvironmentVariable("GITHUB_SERVER_URL");
        var repository = Environment.GetEnvironmentVariable(
            "GITHUB_REPOSITORY");
        return server is null || repository is null
            ? null
            : $"{server.TrimEnd('/')}/{repository}";
    }

    private static void ValidateEnum<T>(T value, string name)
        where T : struct, Enum
    {
        if (Enum.IsDefined(value))
        {
            return;
        }

        throw new InvalidDataException(
            $"Pipeline:{name} is invalid.");
    }

    /// <summary>
    /// Contains the resolved profile and validated context.
    /// </summary>
    /// <param name="Profile">The resolved profile.</param>
    /// <param name="Context">The validated context.</param>
    internal sealed record ResolvedExecution(
        PipelineProfile Profile,
        PipelineExecutionContext? Context);
}