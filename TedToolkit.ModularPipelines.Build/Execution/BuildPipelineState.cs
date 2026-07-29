// -----------------------------------------------------------------------
// <copyright file="BuildPipelineState.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines.Build.Inputs;

namespace TedToolkit.ModularPipelines.Build.Execution;

/// <summary>
/// Represents shared state and failure gating across the registered Build modules.
/// </summary>
/// <param name="engine">The shared failure-safe execution engine.</param>
internal sealed class BuildPipelineState(BuildExecutionEngine engine)
{
    private BuildResult? _result;

    /// <summary>
    /// Runs the recovery, cleanup, and optional transaction-begin boundary.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels initialization.</param>
    /// <returns>Whether initialization succeeded.</returns>
    public Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            () => engine.RecoverAndCleanAsync(cancellationToken));
    }

    /// <summary>
    /// Runs resource preparation when earlier stages succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels resource I/O.</param>
    /// <returns>Whether preparation succeeded.</returns>
    public Task<bool> PrepareResourcesAsync(CancellationToken cancellationToken)
    {
        return ExecuteGatedAsync(
            () => engine.PrepareResourcesAsync(cancellationToken));
    }

    /// <summary>
    /// Runs explicitly enabled formatting when earlier stages succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels formatting.</param>
    /// <returns>Whether formatting succeeded or was skipped.</returns>
    public Task<bool> FormatAsync(CancellationToken cancellationToken)
    {
        return ExecuteGatedAsync(
            () => engine.RunFormatAsync(cancellationToken));
    }

    /// <summary>
    /// Runs build targets when earlier stages succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels builds.</param>
    /// <returns>Whether builds succeeded.</returns>
    public Task<bool> BuildAsync(CancellationToken cancellationToken)
    {
        return ExecuteGatedAsync(
            () => engine.RunBuildTargetsAsync(cancellationToken));
    }

    /// <summary>
    /// Runs test targets when compilation succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels tests.</param>
    /// <returns>Whether tests succeeded.</returns>
    public Task<bool> TestAsync(CancellationToken cancellationToken)
    {
        return ExecuteGatedAsync(
            () => engine.RunTestTargetsAsync(cancellationToken));
    }

    /// <summary>
    /// Runs optional description generation when checks succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels generation.</param>
    /// <returns>Whether generation succeeded or was allowed to continue.</returns>
    public Task<bool> DescribeAsync(CancellationToken cancellationToken)
    {
        return ExecuteGatedAsync(
            () => engine.GenerateDescriptionAsync(cancellationToken));
    }

    /// <summary>
    /// Runs explicit package producers when assertions succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels packing.</param>
    /// <returns>Whether packing succeeded.</returns>
    public Task<bool> PackAsync(CancellationToken cancellationToken)
    {
        if (engine.Profile is not (
            BuildExecutionProfile.Pack
            or BuildExecutionProfile.Publish))
        {
            return Task.FromResult(true);
        }

        return ExecuteGatedAsync(
            () => engine.RunPackTargetsAsync(cancellationToken));
    }

    /// <summary>
    /// Runs local publish/archive producers when assertions and packing succeeded.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels publishing.</param>
    /// <returns>Whether local publishing succeeded.</returns>
    public Task<bool> PublishAsync(CancellationToken cancellationToken)
    {
        if (engine.Profile != BuildExecutionProfile.Publish)
        {
            return Task.FromResult(true);
        }

        return ExecuteGatedAsync(
            () => engine.RunPublishTargetsAsync(cancellationToken));
    }

    /// <summary>
    /// Inventories durable artifacts even when an earlier producer failed.
    /// </summary>
    /// <returns>Whether artifact collection succeeded.</returns>
    public Task<bool> CollectAsync()
    {
        return ExecuteAsync(
            () => engine.CollectArtifactsAsync(CancellationToken.None));
    }

    /// <summary>
    /// Gets a value indicating whether dependent producers may continue.
    /// </summary>
    public bool CanContinue
    {
        get
        {
            return engine.CanContinue;
        }
    }

    /// <summary>
    /// Restores version state and commits the final result and manifest exactly once.
    /// </summary>
    /// <returns>The final Build result.</returns>
    public async Task<BuildResult> FinishAsync()
    {
        _result ??= await engine.FinishAsync(CancellationToken.None)
            .ConfigureAwait(false);
        return _result;
    }

    private async Task<bool> ExecuteGatedAsync(Func<Task> action)
    {
        if (!engine.CanContinue)
        {
            return false;
        }

        return await ExecuteAsync(action)
            .ConfigureAwait(false);
    }

    private async Task<bool> ExecuteGatedAsync(Func<Task<bool>> action)
    {
        if (!engine.CanContinue)
        {
            return false;
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception exception) when (
            IsRecoverableStageException(exception))
        {
            engine.RecordStageFailure(exception);
            return false;
        }
    }

    private async Task<bool> ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (
            IsRecoverableStageException(exception))
        {
            engine.RecordStageFailure(exception);
            return false;
        }
    }

    private static bool IsRecoverableStageException(Exception exception)
    {
        return exception is OperationCanceledException
            or IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or InvalidOperationException
            or TimeoutException;
    }
}