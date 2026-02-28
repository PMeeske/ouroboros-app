namespace Ouroboros.Application.Extensions;

/// <summary>
/// Extension methods for safe fire-and-forget task execution.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Static event raised when a fire-and-forget task faults.
    /// The CLI layer subscribes to this to route exceptions into Iaret's kernel.
    /// Parameters: (Exception exception, string? context).
    /// </summary>
    public static event Action<Exception, string?>? ExceptionObserved;

    /// <summary>
    /// Observes exceptions from a fire-and-forget task to prevent silent failures.
    /// Logs faulted tasks via Debug.WriteLine and raises <see cref="ExceptionObserved"/>
    /// so the agent kernel can react.
    /// </summary>
    public static void ObserveExceptions(this Task task, string? context = null)
    {
        task.ContinueWith(
            t =>
            {
                var ex = t.Exception;
                if (ex == null) return;

                var ctx = context != null ? $" [{context}]" : "";
                System.Diagnostics.Debug.WriteLine(
                    $"Fire-and-forget fault{ctx}: {ex.GetBaseException().Message}");

                try
                {
                    ExceptionObserved?.Invoke(ex, context);
                }
                catch
                {
                    // Never let the observer callback crash the continuation.
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
