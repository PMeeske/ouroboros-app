namespace Ouroboros.Application.Personality;

/// <summary>
/// Interface for executing background operations triggered by thoughts.
/// </summary>
public interface IBackgroundOperationExecutor
{
    /// <summary>Gets the name of this executor.</summary>
    string Name { get; }

    /// <summary>Gets the operation types this executor can handle.</summary>
    IReadOnlyList<string> SupportedOperations { get; }

    /// <summary>Determines if this executor should run for the given thought type.</summary>
    bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context);

    /// <summary>Executes the background operation.</summary>
    Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default);
}