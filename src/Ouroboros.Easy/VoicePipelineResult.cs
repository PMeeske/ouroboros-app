namespace Ouroboros.Easy;

/// <summary>
/// Represents the result of a voice-enabled pipeline execution.
/// </summary>
public sealed class VoicePipelineResult
{
    /// <summary>
    /// Gets whether the pipeline execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the text output from the pipeline, or null if execution failed.
    /// </summary>
    public string? TextOutput { get; }

    /// <summary>
    /// Gets the path to the generated audio file, or null if voice output was not requested or failed.
    /// </summary>
    public string? AudioPath { get; }

    /// <summary>
    /// Gets the error message if execution failed, or null if successful.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a warning message if the main execution succeeded but voice generation failed.
    /// </summary>
    public string? Warning { get; }

    private VoicePipelineResult(bool isSuccess, string? textOutput, string? audioPath, string? error, string? warning = null)
    {
        IsSuccess = isSuccess;
        TextOutput = textOutput;
        AudioPath = audioPath;
        Error = error;
        Warning = warning;
    }

    internal static VoicePipelineResult Success(string textOutput, string? audioPath, string? warning = null)
    {
        return new VoicePipelineResult(true, textOutput, audioPath, null, warning);
    }

    internal static VoicePipelineResult Failure(string error)
    {
        return new VoicePipelineResult(false, null, null, error);
    }
}