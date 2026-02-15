using System.Text;
using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Service for handling voice integration
/// </summary>
public interface IVoiceIntegrationService
{
    /// <summary>
    /// Handles voice command recognition and execution
    /// </summary>
    Task HandleVoiceCommandAsync(string commandName, string[] arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if voice recognition is available
    /// </summary>
    Task<bool> IsVoiceRecognitionAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Recognizes speech and converts to command arguments
    /// </summary>
    Task<string[]> RecognizeSpeechAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of voice integration service
/// </summary>
public class VoiceIntegrationService : IVoiceIntegrationService
{
    private readonly VoiceModeService _voiceModeService;
    private readonly ILogger<VoiceIntegrationService> _logger;
    private readonly ISpectreConsoleService _console;
    
    public VoiceIntegrationService(
        VoiceModeService voiceModeService,
        ILogger<VoiceIntegrationService> logger,
        ISpectreConsoleService console)
    {
        _voiceModeService = voiceModeService;
        _logger = logger;
        _console = console;
    }
    
    public async Task HandleVoiceCommandAsync(string commandName, string[] arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if voice recognition is available
            if (!await IsVoiceRecognitionAvailableAsync(cancellationToken))
            {
                _console.MarkupLine("[yellow]Voice recognition not available. Falling back to text mode.[/]");
                return;
            }
            
            await _voiceModeService.InitializeAsync();
            
            // Use the existing voice mode service for speech recognition
            _console.MarkupLine($"[green]Voice mode activated for {commandName}[/]");
            
            // Recognize speech
            var recognizedArgs = await RecognizeSpeechAsync(cancellationToken);
            
            if (recognizedArgs.Length > 0)
            {
                _console.MarkupLine($"[blue]Recognized: {string.Join(" ", recognizedArgs)}[/]");
                
                // Re-invoke the command with recognized arguments
                // This prevents infinite recursion by not re-adding the --voice flag
                var newArgs = new List<string> { commandName };
                newArgs.AddRange(recognizedArgs);
                
                // TODO: Invoke the command handler with new arguments
                // This would require integration with System.CommandLine
            }
            else
            {
                _console.MarkupLine("[yellow]No speech recognized. Using original arguments.[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling voice command");
            _console.MarkupLine($"[red]Voice command failed: {ex.Message}[/]");
        }
    }
    
    public async Task<bool> IsVoiceRecognitionAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _voiceModeService.InitializeAsync();
            return _voiceModeService.HasStt;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice recognition check failed");
            return false;
        }
    }
    
    public async Task<string[]> RecognizeSpeechAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _console.MarkupLine("[blue]Listening for voice input...[/]");
            
            var input = await _voiceModeService.GetInputAsync("Speak now: ");
            
            if (!string.IsNullOrWhiteSpace(input))
            {
                // Convert speech to command arguments
                // Simple parsing for now - could be enhanced with NLP
                return ParseSpeechToArguments(input);
            }
            
            return Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech recognition failed");
            return Array.Empty<string>();
        }
    }
    
    private static string[] ParseSpeechToArguments(string speech)
    {
        // Simple parsing - split by spaces and handle quoted arguments
        var args = new List<string>();
        var currentArg = new StringBuilder();
        var inQuotes = false;
        
        foreach (char c in speech)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                if (!inQuotes && currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(c);
            }
        }
        
        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }
        
        return args.ToArray();
    }
}