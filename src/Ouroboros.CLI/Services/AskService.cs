using System.Diagnostics;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Microsoft.Extensions.Logging;
using Ouroboros.Diagnostics;
using Ouroboros.Options;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of IAskService that wraps the existing AskCommands functionality
/// </summary>
public class AskService : IAskService
{
    private readonly ILogger<AskService> _logger;

    public AskService(ILogger<AskService> logger)
    {
        _logger = logger;
    }

    public async Task<string> AskAsync(string question, bool useRag = false)
    {
        _logger.LogInformation("Processing question with RAG: {UseRag}", useRag);
        
        // Create options that match what AskCommands.RunAskAsync expects
        var options = new AskOptions
        {
            Question = question,
            Rag = useRag,
            // Set default values for other required properties
            Model = "llama3", // Default model
            Embed = "all-MiniLM-L6-v2", // Default embedding model
            K = 3, // Default number of results
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

        // Capture console output
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Execute the existing AskCommands logic
            await AskCommands.RunAskAsync(options);
            
            // Get the output
            var output = stringWriter.ToString();
            
            // Extract the answer (remove timing information)
            var lines = output.Split('\n');
            var answerLines = lines.Where(line => !line.StartsWith("[timing]") && !string.IsNullOrWhiteSpace(line)).ToList();
            
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
        }
    }
}
