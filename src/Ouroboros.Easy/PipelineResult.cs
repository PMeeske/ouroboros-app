namespace Ouroboros.Easy;

/// <summary>
/// Represents the result of a pipeline execution.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// Gets whether the pipeline execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the output from the pipeline, or null if execution failed.
    /// </summary>
    public string? Output { get; }

    /// <summary>
    /// Gets the error message if execution failed, or null if successful.
    /// </summary>
    public string? Error { get; }

    private PipelineResult(bool isSuccess, string? output, string? error)
    {
        IsSuccess = isSuccess;
        Output = output;
        Error = error;
    }

    internal static PipelineResult Success(string output)
    {
        return new PipelineResult(true, output, null);
    }

    internal static PipelineResult Failure(string error)
    {
        return new PipelineResult(false, null, error);
    }

    /// <summary>
    /// Returns the output if successful, otherwise throws an exception with the error message.
    /// </summary>
    public string GetOutputOrThrow()
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException($"Pipeline execution failed: {Error}");
        }

        return Output!;
    }
}