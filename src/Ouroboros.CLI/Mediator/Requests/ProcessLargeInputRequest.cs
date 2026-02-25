using MediatR;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR query that processes a large input string (e.g., a document or batch
/// of text) through the agent's chunked-processing pipeline.
/// </summary>
public sealed record ProcessLargeInputRequest(string Task, string LargeInput) : IRequest<string>;
