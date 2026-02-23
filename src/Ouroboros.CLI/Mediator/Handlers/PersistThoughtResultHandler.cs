using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="PersistThoughtResultRequest"/>.
/// Persists the result of a thought execution (action taken, response generated, etc).
/// </summary>
public sealed class PersistThoughtResultHandler : IRequestHandler<PersistThoughtResultRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public PersistThoughtResultHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(PersistThoughtResultRequest request, CancellationToken ct)
    {
        var thoughtPersistence = _agent.MemorySub.ThoughtPersistence;
        if (thoughtPersistence == null) return Unit.Value;

        var neuroStore = thoughtPersistence.AsNeuroSymbolicStore();
        if (neuroStore == null) return Unit.Value;

        try
        {
            var sessionId = $"ouroboros-{_agent.Config.Persona.ToLowerInvariant()}";
            var result = new Ouroboros.Domain.Persistence.ThoughtResult(
                Id: Guid.NewGuid(),
                ThoughtId: request.ThoughtId,
                ResultType: request.ResultType,
                Content: request.Content,
                Success: request.Success,
                Confidence: request.Confidence,
                CreatedAt: DateTime.UtcNow,
                ExecutionTime: request.ExecutionTime);

            await neuroStore.SaveResultAsync(sessionId, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtResult] Failed to save: {ex.Message}");
        }

        return Unit.Value;
    }
}
