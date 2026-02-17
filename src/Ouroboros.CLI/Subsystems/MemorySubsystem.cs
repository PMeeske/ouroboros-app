// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Abstractions.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Manages skills, personality, MeTTa reasoning, neural memory, and thought persistence.
/// </summary>
public interface IMemorySubsystem : IAgentSubsystem
{
    ISkillRegistry? Skills { get; }
    PersonalityEngine? PersonalityEngine { get; }
    PersonalityProfile? Personality { get; }
    IValenceMonitor? ValenceMonitor { get; }
    IMeTTaEngine? MeTTaEngine { get; }
    ThoughtPersistenceService? ThoughtPersistence { get; }
    List<InnerThought> PersistentThoughts { get; }
    string? LastThoughtContent { get; set; }
    QdrantNeuralMemory? NeuralMemory { get; }
    List<string> ConversationHistory { get; }
}

/// <summary>
/// Memory subsystem implementation owning skills, personality, symbolic reasoning, and persistence.
/// </summary>
public sealed class MemorySubsystem : IMemorySubsystem
{
    public string Name => "Memory";
    public bool IsInitialized { get; private set; }

    // Skills & Personality
    public ISkillRegistry? Skills { get; set; }
    public PersonalityEngine? PersonalityEngine { get; set; }
    public PersonalityProfile? Personality { get; set; }
    public IValenceMonitor? ValenceMonitor { get; set; }

    // Symbolic reasoning
    public IMeTTaEngine? MeTTaEngine { get; set; }

    // Persistent memory
    public ThoughtPersistenceService? ThoughtPersistence { get; set; }
    public List<InnerThought> PersistentThoughts { get; set; } = new();
    public string? LastThoughtContent { get; set; }

    // Neural memory
    public QdrantNeuralMemory? NeuralMemory { get; set; }

    // Conversation history
    public List<string> ConversationHistory { get; } = new();

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        // ── MeTTa ──
        if (ctx.Config.EnableMeTTa)
        {
            try
            {
                MeTTaEngine ??= new InMemoryMeTTaEngine();
                ctx.Output.RecordInit("MeTTa", true, "symbolic reasoning engine");
            }
            catch (Exception ex) { Console.WriteLine($"  \u26a0 MeTTa unavailable: {ex.Message}"); }
        }
        else
        {
            ctx.Output.RecordInit("MeTTa", false, "disabled");
        }

        // ── Neural Memory ──
        await InitializeNeuralMemoryCoreAsync(ctx);

        // ── Skills ──
        if (ctx.Config.EnableSkills)
            await InitializeSkillsCoreAsync(ctx);
        else
            ctx.Output.RecordInit("Skills", false, "disabled");

        // ── Personality ──
        if (ctx.Config.EnablePersonality)
            await InitializePersonalityCoreAsync(ctx);
        else
            ctx.Output.RecordInit("Personality", false, "disabled");

        // ── Persistent Thoughts ──
        await InitializePersistentThoughtsCoreAsync(ctx);

        MarkInitialized();
    }

    private async Task InitializeNeuralMemoryCoreAsync(SubsystemInitContext ctx)
    {
        var embedding = ctx.Models.Embedding;
        if (embedding == null || string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
        {
            ctx.Output.RecordInit("Neural Memory", false, "requires embeddings + Qdrant");
            return;
        }

        try
        {
            var qdrantRest = ctx.Config.QdrantEndpoint.Replace(":6334", ":6333");
            NeuralMemory = new QdrantNeuralMemory(qdrantRest);
            NeuralMemory.EmbedFunction = async (text, ct) => await embedding.CreateEmbeddingsAsync(text);
            var testEmbed = await embedding.CreateEmbeddingsAsync("test");
            await NeuralMemory.InitializeAsync(testEmbed.Length);
            var stats = await NeuralMemory.GetStatsAsync();
            ctx.Output.RecordInit("Neural Memory", true, $"Qdrant @ {qdrantRest}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    Messages: {stats.NeuronMessagesCount} | Intentions: {stats.IntentionsCount} | Memories: {stats.MemoriesCount}");
            Console.ResetColor();
        }
        catch (HttpRequestException httpEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Neural Memory: Connection failed - {httpEx.Message}");
            Console.WriteLine($"    \u2192 Check if Qdrant is running at {ctx.Config.QdrantEndpoint}");
            Console.ResetColor();
            NeuralMemory = null;
        }
        catch (TimeoutException timeoutEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Neural Memory: Timeout - {timeoutEx.Message}");
            Console.WriteLine($"    \u2192 Qdrant may be overloaded or starting up");
            Console.ResetColor();
            NeuralMemory = null;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Neural Memory: {ex.GetType().Name} - {ex.Message}");
            if (ctx.Config.Debug)
                Console.WriteLine($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
            Console.ResetColor();
            NeuralMemory = null;
        }
    }

    private async Task InitializeSkillsCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var embedding = ctx.Models.Embedding;
            if (embedding != null)
            {
                try
                {
                    var qdrantConfig = new QdrantSkillConfig { ConnectionString = ctx.Config.QdrantEndpoint };
                    var qdrantSkills = new QdrantSkillRegistry(embedding, qdrantConfig);
                    await qdrantSkills.InitializeAsync();
                    Skills = qdrantSkills;
                    var stats = qdrantSkills.GetStats();
                    ctx.Output.RecordInit("Skills", true, $"Qdrant ({stats.TotalSkills} skills loaded)");
                }
                catch (HttpRequestException qdrantConnEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  \u26a0 Qdrant skills: Connection failed - {qdrantConnEx.Message}");
                    Console.ResetColor();
                    Skills = new SkillRegistry(embedding);
                    ctx.Output.RecordInit("Skills", true, "in-memory with embeddings");
                }
                catch (Exception qdrantEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  \u26a0 Qdrant skills failed: {qdrantEx.GetType().Name} - {qdrantEx.Message}");
                    Console.ResetColor();
                    Skills = new SkillRegistry(embedding);
                    ctx.Output.RecordInit("Skills", true, "in-memory with embeddings");
                }
            }
            else
            {
                Skills = new SkillRegistry();
                ctx.Output.RecordInit("Skills", true, "in-memory basic");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            ctx.Output.RecordInit("Skills", false, $"critical: {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
            Skills = new SkillRegistry();
        }
    }

    private async Task InitializePersonalityCoreAsync(SubsystemInitContext ctx)
    {
        try
        {
            var metta = new InMemoryMeTTaEngine();
            var embedding = ctx.Models.Embedding;

            PersonalityEngine = (embedding != null && !string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
                ? new PersonalityEngine(metta, embedding, ctx.Config.QdrantEndpoint)
                : new PersonalityEngine(metta);

            await PersonalityEngine.InitializeAsync();

            var persona = ctx.VoiceService.ActivePersona;
            Personality = PersonalityEngine.GetOrCreateProfile(
                persona.Name, persona.Traits, persona.Moods, persona.CoreIdentity);

            ctx.Output.RecordInit("Personality", true, $"{persona.Name} ({Personality.Traits.Count} traits)");

            ValenceMonitor = new ValenceMonitor();
            ctx.Output.RecordInit("Valence Monitor", true);
        }
        catch (ArgumentException argEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Personality configuration error: {argEx.Message}");
            Console.ResetColor();
        }
        catch (InvalidOperationException opEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Personality engine state error: {opEx.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  \u26a0 Personality engine failed: {ex.GetType().Name} - {ex.Message}");
            if (ctx.Config.Debug)
                Console.WriteLine($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
            Console.ResetColor();
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

                ThoughtPersistence = await ThoughtPersistenceService.CreateWithQdrantAsync(
                    sessionId, ctx.Config.QdrantEndpoint, embeddingFunc);
                ctx.Output.RecordInit("Persistent Memory", true, "Qdrant-backed thought map");
            }
            catch (Exception qdrantEx)
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
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    Thought types: {string.Join(", ", thoughtTypes)}");
                Console.ResetColor();
            }
            else
            {
                ctx.Output.RecordInit("Persistent Memory", true, "ready (first session)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Persistent memory unavailable: {ex.Message}");
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

    public ValueTask DisposeAsync()
    {
        MeTTaEngine?.Dispose();
        IsInitialized = false;
        return ValueTask.CompletedTask;
    }
}
