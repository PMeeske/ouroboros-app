using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR command that runs a connectivity test (llm, metta, embedding, or all).
/// </summary>
public sealed record RunTestRequest(string TestSpec) : IRequest<string>;
