using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IOrchestratorService that wraps the existing orchestrator functionality.
/// Uses a semaphore to prevent concurrent Console.SetOut calls.
/// </summary>
public class OrchestratorService : IOrchestratorService
{
    private static readonly SemaphoreSlim s_consoleLock = new(1, 1);
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(ILogger<OrchestratorService> logger)
    {
        _logger = logger;
    }

    public async Task<string> OrchestrateAsync(string goal)
    {
        _logger.LogInformation("Orchestrating models for goal: {Goal}", goal);

        var options = new OrchestratorOptions
        {
            Goal = goal,
            Debug = false,
            Voice = false,
            VoiceOnly = false,
            VoiceLoop = false,
            LocalTts = false,
            Persona = "Ouroboros",
            Model = "llama3",
            Endpoint = "http://localhost:11434",
            Temperature = 0.7f,
            MaxTokens = 2048,
            TimeoutSeconds = 60
        };

        await s_consoleLock.WaitAsync();
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        try
        {
            Console.SetOut(stringWriter);
            await OrchestratorCommands.RunOrchestratorAsync(options);

            var output = stringWriter.ToString();
            var lines = output.Split('\n');
            var resultLines = lines
                .Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line))
                .ToList();
            return string.Join("\n", resultLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error orchestrating models for goal: {Goal}", goal);
            return $"Error: {ex.Message}";
        }
        finally
        {
            Console.SetOut(originalOut);
            s_consoleLock.Release();
        }
    }
}
