namespace Ouroboros.Application.Extensions;

/// <summary>
/// Extension methods for safe fire-and-forget task execution.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Observes exceptions from a fire-and-forget task to prevent silent failures.
    /// Logs faulted tasks via Debug.WriteLine. Does not re-throw.
    /// </summary>
    public static void ObserveExceptions(this Task task, string? context = null)
    {
        task.ContinueWith(
            t =>
            {
                var ctx = context != null ? $" [{context}]" : "";
                System.Diagnostics.Debug.WriteLine(
                    $"Fire-and-forget fault{ctx}: {t.Exception?.GetBaseException().Message}");
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
