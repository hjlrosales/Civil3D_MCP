using System.Collections.Concurrent;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3D.Bridge.Diagnostics;

namespace Civil3D.Bridge.Execution;

/// <summary>
/// Real application-context implementation: queues tool work and executes it on the AutoCAD
/// application context through the <c>Application.Idle</c> event, which always fires on the main
/// thread. (The previous implementation used <c>DocumentManager.ExecuteInApplicationContext</c>,
/// but in AutoCAD 2025 — an in-process .NET 8 host — its callback ran on a thread-pool thread,
/// so Autodesk API access happened off the main thread and corrupted the WPF ribbon, hanging the
/// tool dispatcher. The idle-queue is FIFO, matching the tool dispatcher's serialization, and
/// tools complete synchronously in practice so the UI is never blocked for long.)
/// </summary>
public sealed class AutodeskApplicationContext : IApplicationContext
{
    private readonly ConcurrentQueue<Func<Task>> _queue = new();
    private readonly object _subscribeSync = new();
    private bool _subscribed;

    /// <inheritdoc />
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(async () =>
        {
            try
            {
                completion.TrySetResult(await action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task.WaitAsync(cancellationToken);
    }

    private void Enqueue(Func<Task> work)
    {
        _queue.Enqueue(work);
        EnsureSubscribed();
    }

    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }

        lock (_subscribeSync)
        {
            if (_subscribed)
            {
                return;
            }

            Application.Idle += OnApplicationIdle;
            _subscribed = true;
            Diag.Log("Subscribed to Application.Idle");
        }
    }

    private void OnApplicationIdle(object? sender, EventArgs e)
    {
        Diag.Log("Application.Idle fired");
        SynchronizationContext? previous = SynchronizationContext.Current;
        try
        {
            // Execute queued tools to completion on the main thread. The bridge tools complete
            // synchronously (their awaits are all on already-completed tasks), so this does not
            // freeze the UI meaningfully. Clearing the sync context prevents any continuation
            // from being posted back to this (blocked) main thread if a tool ever does yield.
            SynchronizationContext.SetSynchronizationContext(null);
            while (_queue.TryDequeue(out Func<Task>? work))
            {
                try
                {
                    work().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Diag.Log("OnIdle work failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
