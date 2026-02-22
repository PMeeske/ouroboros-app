namespace Ouroboros.CLI.Abstractions;

/// <summary>
/// Marker interface for command handlers that require no domain-specific parameters.
/// Enables uniform DI discovery and documents the consistent handler contract.
/// </summary>
public interface ICommandHandler
{
    Task<int> HandleAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed command handler for commands that accept a configuration object.
/// </summary>
/// <typeparam name="TConfig">The configuration/parameter type for this handler.</typeparam>
public interface ICommandHandler<in TConfig>
{
    Task<int> HandleAsync(TConfig config, CancellationToken cancellationToken = default);
}
