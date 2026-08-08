using System;
using System.IO;
using System.Threading;

namespace Civil3D.Bridge.Diagnostics;

/// <summary>Temporary diagnostic logger writing to %TEMP%\bridge-diag.log (diagnosis only).</summary>
internal static class Diag
{
    private static readonly object Sync = new();

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                string line = string.Format(
                    "{0:HH:mm:ss.fff} [t{1}/{2}] {3}{4}",
                    DateTime.Now,
                    Thread.CurrentThread.ManagedThreadId,
                    Thread.CurrentThread.IsThreadPoolThread ? "pool" : "main",
                    message,
                    Environment.NewLine);
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "bridge-diag.log"), line);
            }
        }
        catch
        {
            // Diagnostics must never break the bridge.
        }
    }
}
