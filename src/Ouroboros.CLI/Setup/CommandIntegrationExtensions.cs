namespace Ouroboros.CLI;

/// <summary>
/// Extension methods for integrating Ouroboros into CLI commands.
/// </summary>
public static class CommandIntegrationExtensions
{
    /// <summary>
    /// Wraps a CLI command with Ouroboros integration.
    /// Ensures telemetry and consciousness integration.
    /// </summary>
    public static async Task WithOuroborosIntegrationAsync(
        this Task commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;

        try
        {
            // Broadcast command start to consciousness
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName}",
                "CLI");

            await commandTask;
            success = true;

            // Broadcast completion
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }

    /// <summary>
    /// Wraps a CLI command with Ouroboros integration and returns result.
    /// </summary>
    public static async Task<T> WithOuroborosIntegrationAsync<T>(
        this Task<T> commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;
        T result;

        try
        {
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName} - {description}",
                "CLI");

            result = await commandTask;
            success = true;

            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");

            return result;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }
}