using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IPipelineService that wraps the existing pipeline functionality.
/// Uses a semaphore to prevent concurrent Console.SetOut calls.
/// </summary>
public class PipelineService : IPipelineService
{
    private static readonly SemaphoreSlim s_consoleLock = new(1, 1);
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(ILogger<PipelineService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExecutePipelineAsync(string dsl)
    {
        _logger.LogInformation("Executing pipeline DSL: {Dsl}", dsl);

        var options = new PipelineOptions
        {
            Dsl = dsl,
            Debug = false,
            Voice = false,
            VoiceOnly = false,
            VoiceLoop = false,
            LocalTts = false,
            Persona = "Ouroboros"
        };

        await s_consoleLock.WaitAsync();
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        try
        {
            Console.SetOut(stringWriter);
            await PipelineCommands.RunPipelineAsync(options);

            var output = stringWriter.ToString();
            var lines = output.Split('\n');
            var resultLines = lines
                .Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line))
                .ToList();
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
            s_consoleLock.Release();
        }
    }
}
