namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Specification for a message handler in a neuron (alternate form for code generation).
/// </summary>
public record MessageHandlerSpec(
    string Topic,
    string HandlerName,
    string Description,
    string InputType,
    string OutputType);