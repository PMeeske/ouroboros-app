using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IAskService that wraps the existing AskCommands functionality.
/// Uses a semaphore to prevent concurrent Console.SetOut calls.
/// </summary>
public class AskService : IAskService
{
    private static readonly SemaphoreSlim s_consoleLock = new(1, 1);
    private readonly ILogger<AskService> _logger;

    public AskService(ILogger<AskService> logger)
    {
        _logger = logger;
    }

    public async Task<string> AskAsync(string question, bool useRag = false)
    {
        _logger.LogInformation("Processing question with RAG: {UseRag}", useRag);

        var options = new AskOptions
        {
            Question = question,
            Rag = useRag,
            Model = "llama3",
            Embed = "all-MiniLM-L6-v2",
            K = 3,
            Temperature = 0.7f,
            MaxTokens = 2048,
            TimeoutSeconds = 60,
            Stream = false,
            Agent = false,
            Voice = false,
            VoiceOnly = false,
            VoiceLoop = false,
            LocalTts = false,
            Persona = "Ouroboros",
            Router = "auto",
            Debug = false,
            StrictModel = false,
            Culture = "en-US"
        };

        await s_consoleLock.WaitAsync();
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        try
        {
            Console.SetOut(stringWriter);
            await AskCommands.RunAskAsync(options);

            var output = stringWriter.ToString();
            var lines = output.Split('\n');
            var answerLines = lines
                .Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line))
                .ToList();
            return string.Join("\n", answerLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question: {Question}", question);
            return $"Error: {ex.Message}";
        }
        finally
        {
            Console.SetOut(originalOut);
            s_consoleLock.Release();
        }
    }
}
