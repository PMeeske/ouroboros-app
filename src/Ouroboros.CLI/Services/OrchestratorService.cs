using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IOrchestratorService that wraps the existing orchestrator functionality
/// </summary>
public class OrchestratorService : IOrchestratorService
{
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(ILogger<OrchestratorService> logger)
    {
        _logger = logger;
    }

    public async Task<string> OrchestrateAsync(string goal)
    {
        _logger.LogInformation("Orchestrating models for goal: {Goal}", goal);
        
        // Create options that match what OrchestratorCommands.RunOrchestratorAsync expects
        var options = new OrchestratorOptions
        {
            Goal = goal,
            // Set default values for other required properties
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

        // Capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Execute the existing OrchestratorCommands logic
            await OrchestratorCommands.RunOrchestratorAsync(options);
            
            // Get the output
            var output = stringWriter.ToString();
            
            // Extract the result (remove timing information)
            var lines = output.Split('\n');
            var resultLines = lines.Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line)).ToList();
            
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
        }
    }
}
