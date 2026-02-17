using System.Diagnostics;

namespace Ouroboros.Application.Integration;

/// <summary>
/// Extension methods for telemetry integration.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Executes an operation with telemetry tracking.
    /// </summary>
    public static async Task<T> WithTelemetryAsync<T>(
        this OuroborosTelemetry telemetry,
        string activityName,
        Func<Activity?, Task<T>> operation,
        Action<Activity?, T>? onSuccess = null,
        Action<Activity?, Exception>? onError = null)
    {
        using var activity = telemetry.StartActivity(activityName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation(activity);
            onSuccess?.Invoke(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            onError?.Invoke(activity, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }
}