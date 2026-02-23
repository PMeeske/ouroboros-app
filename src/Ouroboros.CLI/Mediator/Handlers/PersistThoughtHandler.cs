using MediatR;
using Ouroboros.Application.Personality;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="PersistThoughtRequest"/>.
/// Persists a new thought to storage for future sessions,
/// using neuro-symbolic relations when Qdrant is available.
/// </summary>
public sealed class PersistThoughtHandler : IRequestHandler<PersistThoughtRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public PersistThoughtHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(PersistThoughtRequest request, CancellationToken ct)
    {
        var thoughtPersistence = _agent.MemorySub.ThoughtPersistence;
        if (thoughtPersistence == null) return Unit.Value;

        try
        {
            // Try to use neuro-symbolic persistence with automatic relation inference
            var neuroStore = thoughtPersistence.AsNeuroSymbolicStore();
            if (neuroStore != null)
            {
                var sessionId = $"ouroboros-{_agent.Config.Persona.ToLowerInvariant()}";
                var persisted = ToPersistedThought(request.Thought, request.Topic);
                await neuroStore.SaveWithRelationsAsync(sessionId, persisted, autoInferRelations: true);
            }
            else
            {
                await thoughtPersistence.SaveAsync(request.Thought, request.Topic);
            }

            _agent.MemorySub.PersistentThoughts.Add(request.Thought);

            // Keep only the most recent 100 thoughts in memory
            if (_agent.MemorySub.PersistentThoughts.Count > 100)
            {
                _agent.MemorySub.PersistentThoughts.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Failed to save: {ex.Message}");
        }

        return Unit.Value;
    }

    /// <summary>
    /// Converts an InnerThought to a PersistedThought.
    /// </summary>
    private static Ouroboros.Domain.Persistence.PersistedThought ToPersistedThought(InnerThought thought, string? topic)
    {
        string? metadataJson = null;
        if (thought.Metadata != null && thought.Metadata.Count > 0)
        {
            try
            {
                metadataJson = System.Text.Json.JsonSerializer.Serialize(thought.Metadata);
            }
            catch
            {
                // Ignore
            }
        }

        return new Ouroboros.Domain.Persistence.PersistedThought
        {
            Id = thought.Id,
            Type = thought.Type.ToString(),
            Content = thought.Content,
            Confidence = thought.Confidence,
            Relevance = thought.Relevance,
            Timestamp = thought.Timestamp,
            Origin = thought.Origin.ToString(),
            Priority = thought.Priority.ToString(),
            ParentThoughtId = thought.ParentThoughtId,
            TriggeringTrait = thought.TriggeringTrait,
            Topic = topic,
            Tags = thought.Tags,
            MetadataJson = metadataJson,
        };
    }
}
