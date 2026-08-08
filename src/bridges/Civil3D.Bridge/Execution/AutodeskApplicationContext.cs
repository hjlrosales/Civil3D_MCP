using Autodesk.AutoCAD.ApplicationServices;

namespace Civil3D.Bridge.Execution;

/// <summary>
/// Real application-context implementation: marshals work onto the AutoCAD application context via
/// <c>Application.DocumentManager.ExecuteInApplicationContext</c>. The action starts
/// executing synchronously on the application context (so its first chunk — any Autodesk API access —
/// runs on the main thread); later awaits continue on the thread pool.
/// </summary>
public sealed class AutodeskApplicationContext : IApplicationContext
{
    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.DocumentManager.ExecuteInApplicationContext(
            _ =>
            {
                _ = RunAsync();
            },
            null);

        return await completion.Task.WaitAsync(cancellationToken);

        async Task RunAsync()
        {
            try
            {
                completion.TrySetResult(await action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }
    }
}
