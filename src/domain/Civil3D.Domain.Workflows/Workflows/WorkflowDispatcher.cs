using System.Diagnostics;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// Default <see cref="IWorkflowDispatcher"/>. Runs each workflow through the fixed pipeline:
/// <list type="number">
/// <item>publishes <c>WorkflowStarted</c>;</item>
/// <item>runs every registered validator (failures map to <c>E_VALIDATION_FAILED</c>);</item>
/// <item>checks the granted permission against <see cref="IWorkflow.RequiredPermission"/> (maps to <c>E_PERMISSION_DENIED</c>);</item>
/// <item>executes the handler inside a timeout-linked cancellation envelope (workflow
/// <see cref="IWorkflow.Timeout"/> or a 30-minute default);</item>
/// <item>wraps the outcome in a <see cref="WorkflowResult{TResult}"/> and reports 100%;</item>
/// <item>publishes <c>WorkflowCompleted</c>/<c>WorkflowFailed</c> and logs name, validation result,
/// execution time, cancellation/timeout and correlation/session ids.</item>
/// </list>
/// Handlers and validators come from the container by closed generic type, so workflows register
/// freely without any per-workflow switch. A <c>DomainException</c> thrown by a handler or step
/// passes through unchanged so its stable code maps to the matching protocol error.
/// </summary>
public sealed class WorkflowDispatcher : IWorkflowDispatcher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _services;
    private readonly IDomainEventDispatcher _events;
    private readonly ILogger<WorkflowDispatcher> _logger;

    /// <summary>Creates the dispatcher.</summary>
    /// <param name="services">Container used to resolve handlers and validators by closed generic type.</param>
    /// <param name="events">Domain event dispatcher.</param>
    /// <param name="logger">Logger.</param>
    public WorkflowDispatcher(
        IServiceProvider services,
        IDomainEventDispatcher events,
        ILogger<WorkflowDispatcher> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<WorkflowResult<TResult>> DispatchAsync<TWorkflow, TResult>(
        TWorkflow workflow,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        where TWorkflow : class, IWorkflow<TResult>
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        var timer = Stopwatch.StartNew();
        string correlationId = context.CorrelationId;
        await _events.PublishAsync(
            new WorkflowStarted(workflow.Name, correlationId, context.SessionId), cancellationToken).ConfigureAwait(false);

        try
        {
            // 1. Validation: all registered validators, aggregated; nothing else runs on failure.
            var failures = Validate(workflow);
            if (failures.Count > 0)
            {
                string joined = string.Join("; ", failures.Select(f => $"{f.Field}: {f.Message}"));
                _logger.LogWarning(
                    "Workflow {Workflow} failed validation: {Errors} (correlation {CorrelationId}, session {SessionId}).",
                    workflow.Name, joined, correlationId, context.SessionId);
                throw new WorkflowException(
                    WorkflowErrorCode.ValidationFailed,
                    $"Validation failed for workflow '{workflow.Name}': {joined}");
            }

            context.Progress.Report(5, "Validated");

            // 2. Permission check (enum order = escalation).
            if (workflow.RequiredPermission > context.EffectivePermission)
            {
                _logger.LogWarning(
                    "Workflow {Workflow} requires permission {Required} but only {Granted} was granted (correlation {CorrelationId}).",
                    workflow.Name, workflow.RequiredPermission, context.EffectivePermission, correlationId);
                throw new WorkflowException(
                    WorkflowErrorCode.PermissionDenied,
                    $"Workflow '{workflow.Name}' requires {workflow.RequiredPermission} permission.");
            }

            context.Progress.Report(10, "Checked");

            // 3. Execute the handler inside the timeout/cancellation envelope.
            TResult data = await ExecuteWithTimeoutAsync<TWorkflow, TResult>(workflow, context, cancellationToken).ConfigureAwait(false);

            timer.Stop();
            context.Progress.Report(100, "Complete");
            var result = new WorkflowResult<TResult>(
                data,
                Success: true,
                ErrorCode: null,
                Message: null,
                StartedAtUtc: context.StartedAtUtc,
                FinishedAtUtc: DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Workflow {Workflow} completed in {ExecutionTime} ms, result elapsed {ResultElapsed} (correlation {CorrelationId}, session {SessionId}).",
                workflow.Name, timer.ElapsedMilliseconds, result.Elapsed, correlationId, context.SessionId);
            await _events.PublishAsync(
                new WorkflowCompleted(
                    workflow.Name, correlationId, context.SessionId, timer.ElapsedMilliseconds, result.Elapsed),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (WorkflowException ex)
        {
            timer.Stop();
            await _events.PublishAsync(
                new WorkflowFailed(workflow.Name, correlationId, context.SessionId, ex.Code.ToString(), ex.Message),
                CancellationToken.None).ConfigureAwait(false);
            _logger.LogWarning(
                "Workflow {Workflow} failed with {ErrorCode}: {Message} (correlation {CorrelationId}).",
                workflow.Name, ex.Code, ex.Message, correlationId);
            throw;
        }
        catch (DomainException ex)
        {
            // Domain failures (no document, entity not found, transaction failed, ...) pass
            // through unchanged so the tool layer maps the stable code to the matching protocol
            // error.
            timer.Stop();
            await _events.PublishAsync(
                new WorkflowFailed(workflow.Name, correlationId, context.SessionId, ex.Code.ToString(), ex.Message),
                CancellationToken.None).ConfigureAwait(false);
            _logger.LogWarning(
                "Workflow {Workflow} failed with domain code {ErrorCode}: {Message} (correlation {CorrelationId}).",
                workflow.Name, ex.Code, ex.Message, correlationId);
            throw;
        }
        catch (OperationCanceledException)
        {
            timer.Stop();
            bool cancelledByCaller = cancellationToken.IsCancellationRequested;
            var code = cancelledByCaller ? WorkflowErrorCode.Cancelled : WorkflowErrorCode.Timeout;
            await _events.PublishAsync(
                new WorkflowFailed(workflow.Name, correlationId, context.SessionId, code.ToString(), null),
                CancellationToken.None).ConfigureAwait(false);
            _logger.LogWarning(
                "Workflow {Workflow} was {Reason} (correlation {CorrelationId}).",
                workflow.Name, cancelledByCaller ? "cancelled" : "cancelled by timeout", correlationId);
            throw new WorkflowException(
                code,
                cancelledByCaller
                    ? $"Workflow '{workflow.Name}' was cancelled."
                    : $"Workflow '{workflow.Name}' exceeded its execution timeout.");
        }
        catch (Exception ex)
        {
            timer.Stop();
            await _events.PublishAsync(
                new WorkflowFailed(workflow.Name, correlationId, context.SessionId, "Internal", null),
                CancellationToken.None).ConfigureAwait(false);
            _logger.LogError(ex, "Workflow {Workflow} failed (correlation {CorrelationId}).", workflow.Name, correlationId);
            throw new WorkflowException(
                WorkflowErrorCode.Internal,
                "An unexpected error occurred while executing the workflow.",
                ex);
        }
    }

    private async Task<TResult> ExecuteWithTimeoutAsync<TWorkflow, TResult>(
        TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
        where TWorkflow : class, IWorkflow<TResult>
    {
        IWorkflowHandler<TWorkflow, TResult> handler = ResolveHandler<TWorkflow, TResult>();
        // A non-positive timeout would fire CancelAfter immediately and read as an instant
        // timeout; fall back to the default so misconfigured workflows degrade predictably.
        TimeSpan timeout = DefaultTimeout;
        if (workflow.Timeout is { } configured && configured > TimeSpan.Zero)
        {
            timeout = configured;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        return await handler.HandleAsync(workflow, context, linked.Token).ConfigureAwait(false);
    }

    private IReadOnlyList<ValidationFailure> Validate<TWorkflow>(TWorkflow workflow)
        where TWorkflow : IWorkflow
    {
        Type validatorType = typeof(IWorkflowValidator<>).MakeGenericType(typeof(TWorkflow));
        var validators = (IEnumerable<object>)_services.GetServices(validatorType);

        var failures = new List<ValidationFailure>();
        foreach (object validator in validators)
        {
            var result = ((IWorkflowValidator<TWorkflow>)validator).Validate(workflow);
            if (!result.IsValid)
            {
                failures.AddRange(result.Failures);
            }
        }

        return failures;
    }

    private IWorkflowHandler<TWorkflow, TResult> ResolveHandler<TWorkflow, TResult>()
        where TWorkflow : class, IWorkflow<TResult>
    {
        Type handlerType = typeof(IWorkflowHandler<,>).MakeGenericType(typeof(TWorkflow), typeof(TResult));
        try
        {
            return (IWorkflowHandler<TWorkflow, TResult>)_services.GetRequiredService(handlerType);
        }
        catch (InvalidOperationException ex)
        {
            throw new WorkflowException(
                WorkflowErrorCode.Internal,
                "No handler is registered for workflow '" + typeof(TWorkflow).Name + "'.",
                ex);
        }
    }
}
