// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Abstractions.Agent;
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
/// Memory subsystem implementation owning skills, personality, symbolic reasoning, and persistence.
/// </summary>
public sealed partial class MemorySubsystem : IMemorySubsystem
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

    // Persistent conversation memory (cross-session recall)
    public PersistentConversationMemory? ConversationMemory { get; set; }

    // Self-persistence (mind state storage in Qdrant)
    public SelfPersistence? SelfPersistence { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;

        // ── MeTTa ──
        if (ctx.Config.EnableMeTTa)
        {
            try
            {
                MeTTaEngine ??= new InMemoryMeTTaEngine();
                ctx.Output.RecordInit("MeTTa", true, "symbolic reasoning engine");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ MeTTa unavailable: {ex.Message}")}"); }
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

        // ── Persistent Conversation Memory ──
        await InitializeConversationMemoryCoreAsync(ctx);

        // ── Self-Persistence ──
        await InitializeSelfPersistenceCoreAsync(ctx);

        MarkInitialized();
    }

    private async Task InitializeNeuralMemoryCoreAsync(SubsystemInitContext ctx)
    {
        var embedding = ctx.Models.Embedding;
        // Try DI first, fall back to endpoint string check
        var diMemory = ctx.Services?.GetService<QdrantNeuralMemory>();
        if (embedding == null || (diMemory == null && string.IsNullOrEmpty(ctx.Config.QdrantEndpoint)))
        {
            ctx.Output.RecordInit("Neural Memory", false, "requires embeddings + Qdrant");
            return;
        }

        try
        {
            NeuralMemory = diMemory ?? new QdrantNeuralMemory(ctx.Config.QdrantEndpoint.Replace(":6334", ":6333"));
            NeuralMemory.EmbedFunction = async (text, ct) => await embedding.CreateEmbeddingsAsync(text);
            var testEmbed = await embedding.CreateEmbeddingsAsync("test");
            await NeuralMemory.InitializeAsync(testEmbed.Length);
            var stats = await NeuralMemory.GetStatsAsync();
            Ouroboros.Application.Tools.ServiceContainerFactory.RegisterSingleton(NeuralMemory);
            var settings = ctx.Services?.GetService<QdrantSettings>();
            var label = settings != null ? $"Qdrant @ {settings.HttpEndpoint}" : "Qdrant";
            ctx.Output.RecordInit("Neural Memory", true, label);
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Messages: {stats.NeuronMessagesCount} | Intentions: {stats.IntentionsCount} | Memories: {stats.MemoriesCount}"));
        }
        catch (HttpRequestException httpEx)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Neural Memory: Connection failed - {httpEx.Message}")}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Warn($"→ Check if Qdrant is running at {ctx.Config.QdrantEndpoint}")}");
            NeuralMemory = null;
        }
        catch (TimeoutException timeoutEx)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Neural Memory: Timeout - {timeoutEx.Message}")}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Warn("→ Qdrant may be overloaded or starting up")}");
            NeuralMemory = null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Neural Memory: {ex.GetType().Name} - {ex.Message}")}");
            if (ctx.Config.Debug)
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}"));
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
                    var qdrantClient = ctx.Services?.GetService<QdrantClient>();
                    var registry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
                    var qdrantSettings = ctx.Services?.GetService<QdrantSettings>();
                    QdrantSkillRegistry qdrantSkills;
                    if (qdrantClient != null && registry != null && qdrantSettings != null)
                        qdrantSkills = new QdrantSkillRegistry(qdrantClient, registry, qdrantSettings, embedding);
                    else
                        qdrantSkills = new QdrantSkillRegistry(embedding, new QdrantSkillConfig { ConnectionString = ctx.Config.QdrantEndpoint });
                    await qdrantSkills.InitializeAsync();
                    Skills = qdrantSkills;
                    var stats = qdrantSkills.GetStats();
                    ctx.Output.RecordInit("Skills", true, $"Qdrant ({stats.TotalSkills} skills loaded)");
                }
                catch (HttpRequestException qdrantConnEx)
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Qdrant skills: Connection failed - {qdrantConnEx.Message}")}");
                    Skills = new SkillRegistry(embedding);
                    ctx.Output.RecordInit("Skills", true, "in-memory with embeddings");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception qdrantEx)
                {
                    AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Qdrant skills failed: {qdrantEx.GetType().Name} - {qdrantEx.Message}")}");
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Skills critical: {Markup.Escape($"{ex.GetType().Name}: {ex.Message}")}[/]");
            ctx.Output.RecordInit("Skills", false, $"critical: {ex.GetType().Name}: {ex.Message}");
            Skills = new SkillRegistry();
        }
    }

}
