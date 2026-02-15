using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IPipelineService that wraps the existing pipeline functionality
/// </summary>
public class PipelineService : IPipelineService
{
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(ILogger<PipelineService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecutePipelineAsync(string dsl)
    {
        _logger.LogInformation("Executing pipeline DSL: {Dsl}", dsl);
        
        // Create options that match what PipelineCommands.RunPipelineAsync expects
        var options = new PipelineOptions
        {
            Dsl = dsl,
            // Set default values for other required properties
            Debug = false,
            Voice = false,
            VoiceOnly = false,
            VoiceLoop = false,
            LocalTts = false,
            Persona = "Ouroboros"
        };

        // Capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Execute the existing PipelineCommands logic
            await PipelineCommands.RunPipelineAsync(options);
            
            // Get the output
            var output = stringWriter.ToString();
            
            // Extract the result (remove timing information)
            var lines = output.Split('\n');
            var resultLines = lines.Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line)).ToList();
            
            return string.Join("\n", resultLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing pipeline: {Dsl}", dsl);
            return $"Error: {ex.Message}";
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}