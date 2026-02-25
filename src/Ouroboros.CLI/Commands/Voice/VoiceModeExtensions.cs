namespace Ouroboros.CLI.Commands;

/// <summary>
/// Extension methods for voice mode integration.
/// </summary>
public static class VoiceModeExtensions
{
    /// <summary>
    /// Creates a VoiceModeService from common options pattern.
    /// </summary>
    public static VoiceModeService CreateVoiceService(
        bool voice,
        string persona,
        bool voiceOnly = false,
        bool localTts = false,
        bool voiceLoop = true,
        string model = "llama3",
        string endpoint = "http://localhost:11434",
        string embedModel = "nomic-embed-text",
        string qdrantEndpoint = "http://localhost:6334")
    {
        return new VoiceModeService(new VoiceModeConfig(
            Persona: persona,
            VoiceOnly: voiceOnly,
            LocalTts: localTts,
            VoiceLoop: voiceLoop,
            Model: model,
            Endpoint: endpoint,
            EmbedModel: embedModel,
            QdrantEndpoint: qdrantEndpoint));
    }

    /// <summary>
    /// Runs a command with voice mode wrapper.
    /// </summary>
    public static async Task RunWithVoiceAsync(
        this VoiceModeService voice,
        string commandName,
        Func<string, Task<string>> executeCommand,
        string? initialInput = null)
    {
        await voice.InitializeAsync();
        voice.PrintHeader(commandName);

        // Greeting
        await voice.SayAsync($"Hey there! {commandName} mode is ready. What would you like to do?");

        bool running = true;
        string? lastInput = initialInput;

        while (running)
        {
            // Get input (voice or keyboard)
            string? input = lastInput ?? await voice.GetInputAsync("\n  You: ");
            lastInput = null;

            if (string.IsNullOrWhiteSpace(input)) continue;

            // Check for exit
            if (IsExitCommand(input))
            {
                await voice.SayAsync("Goodbye! It was nice chatting with you.");
                running = false;
                continue;
            }

            // Check for help
            if (input.Equals("help", StringComparison.OrdinalIgnoreCase) || input == "?")
            {
                await voice.SayAsync("You can ask me anything related to " + commandName + ". Say 'exit' or 'goodbye' to quit.");
                continue;
            }

            // Execute the command
            try
            {
                var response = await executeCommand(input);
                await voice.SayAsync(response);
            }
            catch (Exception ex)
            {
                await voice.SayAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
    }

    public static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }
}