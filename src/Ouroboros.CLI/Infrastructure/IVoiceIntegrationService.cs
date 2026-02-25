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