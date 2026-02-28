// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Autonomous;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;
using Spectre.Console;

/// <summary>
/// Continuation of MemorySubsystem: personality initialization, persistent thoughts,
/// conversation memory, self-persistence, thought commands, and disposal.
/// </summary>
public sealed partial class MemorySubsystem
{
    private async Task InitializePersonalityCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var metta = new InMemoryMeTTaEngine();
            var embedding = ctx.Models.Embedding;
            var qdrantClient = ctx.Services?.GetService<QdrantClient>();
            var collectionRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();

            if (embedding != null && qdrantClient != null)
                PersonalityEngine = new PersonalityEngine(metta, embedding, qdrantClient, collectionRegistry);
            else if (embedding != null && !string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
                PersonalityEngine = new PersonalityEngine(metta, embedding, ctx.Config.QdrantEndpoint);
            else
                PersonalityEngine = new PersonalityEngine(metta);

            await PersonalityEngine.InitializeAsync();

            var persona = ctx.VoiceService.ActivePersona;
            Personality = PersonalityEngine.GetOrCreateProfile(
                persona.Name, persona.Traits, persona.Moods, persona.CoreIdentity);

            ctx.Output.RecordInit("Personality", true, $"{persona.Name} ({Personality.Traits.Count} traits)");

            // Restore personality state from previous session's snapshot
            try
            {
                var snapshot = await PersonalityEngine.LoadLatestPersonalitySnapshotAsync(persona.Name);
                if (snapshot != null)
                {
                    // Apply saved trait intensities
                    foreach (var (traitName, intensity) in snapshot.TraitIntensities)
                    {
                        if (Personality.Traits.TryGetValue(traitName, out var existing))
                            Personality.Traits[traitName] = existing with { Intensity = intensity };
                    }

                    // Restore mood and interaction count
                    var restoredMood = new MoodState(
                        snapshot.CurrentMood,
                        Personality.CurrentMood.Energy,
                        Personality.CurrentMood.Positivity,
                        Personality.CurrentMood.TraitModifiers);
                    Personality = Personality with
                    {
                        CurrentMood = restoredMood,
                        AdaptabilityScore = snapshot.AdaptabilityScore,
                        InteractionCount = snapshot.InteractionCount,
                    };

                    ctx.Output.RecordInit("Personality Restore", true,
                        $"restored from {snapshot.Timestamp:g} ({snapshot.InteractionCount} interactions)");
                }
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Personality restore: {Markup.Escape(ex.Message)}")}");
            }
            catch (IOException ex)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Personality restore: {Markup.Escape(ex.Message)}")}");
            }

            ValenceMonitor = new ValenceMonitor();
            ctx.Output.RecordInit("Valence Monitor", true);
        }
        catch (ArgumentException argEx)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Personality configuration error: {argEx.Message}")}");
        }
        catch (InvalidOperationException opEx)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Personality engine state error: {opEx.Message}")}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Personality engine failed: {ex.GetType().Name} - {ex.Message}")}");
            if (ctx.Config.Debug)
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}"));
        }
    }

    private async Task InitializePersistentThoughtsCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var sessionId = $"ouroboros-{ctx.Config.Persona.ToLowerInvariant()}";
            var thoughtsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros", "thoughts");

            try
            {
                Func<string, Task<float[]>>? embeddingFunc = null;
                if (ctx.Models.Embedding != null)
                {
                    embeddingFunc = async (text) => await ctx.Models.Embedding.CreateEmbeddingsAsync(text);
                }

                var tpClient = ctx.Services?.GetService<QdrantClient>();
                var tpRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
                var tpSettings = ctx.Services?.GetService<QdrantSettings>();
                if (tpClient != null && tpRegistry != null && tpSettings != null)
                    ThoughtPersistence = await ThoughtPersistenceService.CreateWithQdrantAsync(
                        sessionId, tpClient, tpRegistry, tpSettings, embeddingFunc);
                else
                    ThoughtPersistence = await ThoughtPersistenceService.CreateWithQdrantAsync(
                        sessionId, ctx.Config.QdrantEndpoint, embeddingFunc);
                ctx.Output.RecordInit("Persistent Memory", true, "Qdrant-backed thought map");
            }
            catch (HttpRequestException qdrantEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Qdrant unavailable: {qdrantEx.Message}, using file storage");
                ThoughtPersistence = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, thoughtsDir);
                ctx.Output.RecordInit("Persistent Memory", true, "file-based (Qdrant unavailable)");
            }
            catch (InvalidOperationException qdrantEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Qdrant unavailable: {qdrantEx.Message}, using file storage");
                ThoughtPersistence = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, thoughtsDir);
                ctx.Output.RecordInit("Persistent Memory", true, "file-based (Qdrant unavailable)");
            }

            PersistentThoughts = (await ThoughtPersistence.GetRecentAsync(50)).ToList();

            if (PersistentThoughts.Count > 0)
            {
                ctx.Output.RecordInit("Persistent Memory", true, $"{PersistentThoughts.Count} thoughts recalled");
                var thoughtTypes = PersistentThoughts
                    .GroupBy(t => t.Type).OrderByDescending(g => g.Count()).Take(3)
                    .Select(g => $"{g.Key}:{g.Count()}");
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Thought types: {string.Join(", ", thoughtTypes)}"));
            }
            else
            {
                ctx.Output.RecordInit("Persistent Memory", true, "ready (first session)");
            }
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Persistent memory unavailable: {ex.Message}")}");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Persistent memory unavailable: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Initializes persistent cross-session conversation memory.
    /// </summary>
    private async Task InitializeConversationMemoryCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var embedding = ctx.Models.Embedding;
            var cmClient = ctx.Services?.GetService<QdrantClient>();
            var cmRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
            if (cmClient != null && cmRegistry != null)
                ConversationMemory = new PersistentConversationMemory(cmClient, cmRegistry, embedding);
            else
                ConversationMemory = new PersistentConversationMemory(
                    embedding,
                    new ConversationMemoryConfig { QdrantEndpoint = ctx.Config.QdrantEndpoint });
            await ConversationMemory.InitializeAsync(ctx.Config.Persona, CancellationToken.None);
            var memStats = ConversationMemory.GetStats();
            if (memStats.TotalSessions > 0)
            {
                ctx.Output.RecordInit("Conversation Memory", true,
                    $"{memStats.TotalSessions} sessions ({memStats.TotalTurns} turns)");
            }
            else
            {
                ctx.Output.RecordInit("Conversation Memory", true, "first session");
            }
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Conversation Memory: {ex.Message}")}");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Conversation Memory: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Initializes self-persistence for mind state storage in Qdrant.
    /// </summary>
    private async Task InitializeSelfPersistenceCoreAsync(SubsystemInitContext ctx)
    {
        var embedding = ctx.Models.Embedding;
        if (embedding == null) return;

        try
        {
            var spSettings = ctx.Services?.GetService<QdrantSettings>();
            if (spSettings != null)
                SelfPersistence = new SelfPersistence(spSettings,
                    async text => await embedding.CreateEmbeddingsAsync(text));
            else
                SelfPersistence = new SelfPersistence(ctx.Config.QdrantEndpoint,
                    async text => await embedding.CreateEmbeddingsAsync(text));
            await SelfPersistence.InitializeAsync(CancellationToken.None);
            ctx.Output.RecordInit("Self-Persistence", true, "Qdrant mind state storage");
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Self-Persistence: {ex.Message}")}");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Self-Persistence: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Saves personality snapshot before shutdown.
    /// </summary>
    public async Task SavePersonalitySnapshotAsync(string personaName)
    {
        if (PersonalityEngine != null)
        {
            await PersonalityEngine.SavePersonalitySnapshotAsync(personaName);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THOUGHT COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the last thought content for "save it" command.
    /// Call this whenever the agent generates a thought/learning.
    /// </summary>
    internal void TrackLastThought(string content)
    {
        LastThoughtContent = content;
    }

    /// <summary>
    /// Direct command to save a thought/learning to persistent memory.
    /// Supports "save it" to save the last generated thought, or explicit content.
    /// </summary>
    internal async Task<string> SaveThoughtCommandAsync(string argument)
    {
        try
        {
            if (ThoughtPersistence == null)
            {
                return "❌ Thought persistence is not initialized. Thoughts cannot be saved.";
            }

            string contentToSave;
            string? topic = null;

            if (string.IsNullOrWhiteSpace(argument))
            {
                // "save it" or "save thought" without argument - use last thought
                if (string.IsNullOrWhiteSpace(LastThoughtContent))
                {
                    return @"❌ No recent thought to save.

💡 **Usage:**
  `save it` - saves the last thought/learning
  `save thought <content>` - saves explicit content
  `save learning <content>` - saves a learning

Example: save thought I discovered that monadic composition simplifies error handling";
                }

                contentToSave = LastThoughtContent;
            }
            else
            {
                contentToSave = argument.Trim();
            }

            // Parse topic if present (format: "content #topic" or "content [topic]")
            var hashIndex = contentToSave.LastIndexOf('#');
            var bracketIndex = contentToSave.LastIndexOf('[');

            if (hashIndex > 0)
            {
                topic = contentToSave[(hashIndex + 1)..].Trim().TrimEnd(']');
                contentToSave = contentToSave[..hashIndex].Trim();
            }
            else if (bracketIndex > 0 && contentToSave.EndsWith(']'))
            {
                topic = contentToSave[(bracketIndex + 1)..^1].Trim();
                contentToSave = contentToSave[..bracketIndex].Trim();
            }

            // Determine thought type based on content
            var thoughtType = InnerThoughtType.Consolidation; // Default for learnings
            if (contentToSave.Contains("learned", StringComparison.OrdinalIgnoreCase) ||
                contentToSave.Contains("discovered", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Consolidation;
            }
            else if (contentToSave.Contains("wonder", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("curious", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("?"))
            {
                thoughtType = InnerThoughtType.Curiosity;
            }
            else if (contentToSave.Contains("feel", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("emotion", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Emotional;
            }
            else if (contentToSave.Contains("idea", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("perhaps", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("maybe", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Creative;
            }
            else if (contentToSave.Contains("think", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("realize", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Metacognitive;
            }

            // Create and save the thought
            var thought = InnerThought.CreateAutonomous(
                thoughtType,
                contentToSave,
                confidence: 0.85,
                priority: ThoughtPriority.Normal,
                tags: topic != null ? [topic] : null);

            await PersistThoughtFunc(thought, topic);

            var typeEmoji = thoughtType switch
            {
                InnerThoughtType.Consolidation => "💡",
                InnerThoughtType.Curiosity => "🤔",
                InnerThoughtType.Emotional => "💭",
                InnerThoughtType.Creative => "💫",
                InnerThoughtType.Metacognitive => "🧠",
                _ => "📝"
            };

            var topicNote = topic != null ? $" (topic: {topic})" : "";
            return $"✅ **Thought Saved**{topicNote}\n\n{typeEmoji} {contentToSave}\n\nType: {thoughtType} | ID: {thought.Id:N}";
        }
        catch (HttpRequestException ex)
        {
            return $"❌ Failed to save thought: {ex.Message}";
        }
        catch (IOException ex)
        {
            return $"❌ Failed to save thought: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Failed to save thought: {ex.Message}";
        }
    }

    /// <summary>
    /// Agent-level persistence callback that uses the full neuro-symbolic pipeline.
    /// Set by the mediator (OuroborosAgent) during wiring.
    /// </summary>
    internal Func<InnerThought, string?, Task> PersistThoughtFunc { get; set; }
        = (thought, topic) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        MeTTaEngine?.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
