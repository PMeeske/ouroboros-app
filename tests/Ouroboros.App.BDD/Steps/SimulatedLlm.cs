using Ouroboros.Abstractions.Core;

namespace Ouroboros.Specs.Steps;

/// <summary>
/// Simulated LLM for testing purposes.
/// </summary>
internal class SimulatedLlm : IChatCompletionModel
{
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Return simulated responses based on prompt content
        if (prompt.Contains("suggest", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(@"1. UseDraft - Generate an initial draft response. This is the first step after SetTopic.
2. UseCritique - Analyze and critique the current draft to identify improvements.
3. UseImprove - Refine the draft based on critique feedback.");
        }

        if (prompt.Contains("build", StringComparison.OrdinalIgnoreCase) && prompt.Contains("DSL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("SetTopic('document analysis') | UseIngest | UseDraft | UseCritique | UseImprove");
        }

        if (prompt.Contains("explain", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("This pipeline starts with a topic, generates a draft, critiques it, and improves the response.");
        }

        if (prompt.Contains("Generate complete, production-quality C#", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(@"```csharp
using System;

namespace Ouroboros.Core;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// </summary>
/// <typeparam name=""TValue"">The type of the success value.</typeparam>
/// <typeparam name=""TError"">The type of the error value.</typeparam>
public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents success.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents failure.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value. Throws if this is a failure result.
    /// </summary>
    public TValue Value => _isSuccess ? _value! : throw new InvalidOperationException(""Cannot access Value of a failed Result"");

    /// <summary>
    /// Gets the error value. Throws if this is a success result.
    /// </summary>
    public TError Error => !_isSuccess ? _error! : throw new InvalidOperationException(""Cannot access Error of a successful Result"");

    private Result(TValue value, TError? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        _isSuccess = isSuccess;
    }

    /// <summary>
    /// Creates a success result with the specified value.
    /// </summary>
    public static Result<TValue, TError> Success(TValue value) => new(value, default, true);

    /// <summary>
    /// Creates a failure result with the specified error.
    /// </summary>
    public static Result<TValue, TError> Failure(TError error) => new(default!, error, false);
}
```");
        }

        return Task.FromResult("Simulated LLM response");
    }
}