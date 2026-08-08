namespace Civil3D.Bridge.Execution;

/// <summary>
/// Abstraction over the host application's main thread. The Bridge implements this with
/// <c>Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.ExecuteInApplicationContext</c>;
/// tests substitute an in-line implementation. All Autodesk API access must flow through it.
/// </summary>
public interface IApplicationContext
{
    /// <summary>Executes the action on the application context and awaits its result.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The work to marshal; must start synchronously on the application context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
}
