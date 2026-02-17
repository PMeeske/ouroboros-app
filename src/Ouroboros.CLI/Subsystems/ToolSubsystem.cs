// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Mcp;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Manages tool registry, dynamic tool creation, smart tool selection, and browser automation.
/// </summary>
public interface IToolSubsystem : IAgentSubsystem
{
    ToolRegistry Tools { get; set; }
    DynamicToolFactory? ToolFactory { get; }
    IntelligentToolLearner? ToolLearner { get; }
    SmartToolSelector? SmartToolSelector { get; set; }
    ToolCapabilityMatcher? ToolCapabilityMatcher { get; set; }
    PlaywrightMcpTool? PlaywrightTool { get; }
    PromptOptimizer PromptOptimizer { get; }

    // Pipeline DSL state
    IReadOnlyDictionary<string, PipelineTokenInfo>? AllPipelineTokens { get; }
    CliPipelineState? PipelineState { get; set; }
}

/// <summary>
/// Tool subsystem implementation owning tool lifecycle and browser automation.
/// </summary>
public sealed class ToolSubsystem : IToolSubsystem
{
    public string Name => "Tools";
    public bool IsInitialized { get; private set; }

    // Core tool management
    public ToolRegistry Tools { get; set; } = new();
    public DynamicToolFactory? ToolFactory { get; set; }
    public IntelligentToolLearner? ToolLearner { get; set; }

    // Smart tool selection
    public SmartToolSelector? SmartToolSelector { get; set; }
    public ToolCapabilityMatcher? ToolCapabilityMatcher { get; set; }

    // Browser automation
    public PlaywrightMcpTool? PlaywrightTool { get; set; }

    // Runtime prompt optimization
    public PromptOptimizer PromptOptimizer { get; } = new();

    // Pipeline DSL state
    public IReadOnlyDictionary<string, PipelineTokenInfo>? AllPipelineTokens { get; set; }
    public CliPipelineState? PipelineState { get; set; }

    // Cross-subsystem context (set during InitializeAsync)
    internal SubsystemInitContext Ctx { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <summary>Tool-aware LLM wrapping the effective chat model with all registered tools.</summary>
    public ToolAwareChatModel? Llm { get; set; }

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;
        if (!ctx.Config.EnableTools)
        {
            Tools = ToolRegistry.CreateDefault();
            ctx.Output.RecordInit("Tools", false, "disabled");
            MarkInitialized();
            return;
        }

        try
        {
            Tools = ToolRegistry.CreateDefault().WithAutonomousTools();

            var chatModel = ctx.Models.ChatModel;
            if (chatModel != null)
            {
                // Bootstrap with temporary tool-aware LLM
                var tempLlm = new ToolAwareChatModel(chatModel, Tools);
                ToolFactory = new DynamicToolFactory(tempLlm);

                Tools = Tools
                    .WithTool(ToolFactory.CreateWebSearchTool("duckduckgo"))
                    .WithTool(ToolFactory.CreateUrlFetchTool())
                    .WithTool(ToolFactory.CreateCalculatorTool());

                // Qdrant admin for self-managing neuro-symbolic memory
                if (!string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
                {
                    var qdrantRest = ctx.Config.QdrantEndpoint.Replace(":6334", ":6333");
                    Func<string, CancellationToken, Task<float[]>>? embedFunc = null;
                    if (ctx.Models.Embedding != null)
                        embedFunc = async (text, ct) => await ctx.Models.Embedding.CreateEmbeddingsAsync(text, ct);
                    var qdrantAdmin = new QdrantAdminTool(qdrantRest, embedFunc);
                    Tools = Tools.WithTool(qdrantAdmin);
                    ctx.Output.RecordInit("Qdrant Admin", true, "self-management tool");
                }

                // Playwright browser automation
                if (ctx.Config.EnableBrowser)
                {
                    try
                    {
                        PlaywrightTool = new PlaywrightMcpTool();
                        await PlaywrightTool.InitializeAsync();
                        Tools = Tools.WithTool(PlaywrightTool);
                        ctx.Output.RecordInit("Playwright", true, $"browser automation ({PlaywrightTool.AvailableTools.Count} tools)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  \u26a0 Playwright: Not available ({ex.Message})");
                    }
                }
                else
                {
                    ctx.Output.RecordInit("Playwright", false, "disabled");
                }

                // Register camera capture tool (cross-cutting - provided by agent)
                try { ctx.RegisterCameraCaptureAction?.Invoke(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Tools] capture_camera registration failed: {ex.Message}"); }

                // Create final ToolAwareChatModel with ALL tools
                var effectiveModel = ctx.Models.OrchestratedModel as Ouroboros.Abstractions.Core.IChatCompletionModel ?? chatModel;
                Llm = new ToolAwareChatModel(effectiveModel, Tools);
                ToolFactory = new DynamicToolFactory(Llm);

                // Tool Learner (needs embedding + MeTTa)
                if (ctx.Models.Embedding != null)
                {
                    var mettaEngine = new InMemoryMeTTaEngine();
                    ctx.Memory.MeTTaEngine = mettaEngine; // Set MeTTa on Memory subsystem

                    ToolLearner = new IntelligentToolLearner(
                        ToolFactory, mettaEngine, ctx.Models.Embedding, Llm, ctx.Config.QdrantEndpoint);
                    await ToolLearner.InitializeAsync();
                    var stats = ToolLearner.GetStats();
                    ctx.Output.RecordInit("Tool Learner", true, $"{stats.TotalPatterns} patterns (GA+MeTTa)");
                }
                else
                {
                    ctx.Output.RecordInit("Tools", true, $"{Tools.Count} registered");
                }

                // Self-introspection tools
                foreach (var tool in SystemAccessTools.CreateAllTools())
                    Tools = Tools.WithTool(tool);
                ctx.Output.RecordInit("Self-Introspection", true, "code tools registered");

                // Roslyn C# analysis tools
                foreach (var tool in RoslynAnalyzerTools.CreateAllTools())
                    Tools = Tools.WithTool(tool);
                ctx.Output.RecordInit("Roslyn", true, "C# analysis tools registered");

                // CRITICAL: Recreate Llm with ALL tools (immutable ToolRegistry)
                Llm = new ToolAwareChatModel(effectiveModel, Tools);
                System.Diagnostics.Debug.WriteLine($"[Tools] Final Llm created with {Tools.Count} tools");
            }
            else
            {
                ctx.Output.RecordInit("Tools", true, $"{Tools.Count} (static only)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Tool factory failed: {ex.Message}");
        }

        // ── Pipeline DSL tokens ──
        try
        {
            AllPipelineTokens = SkillCliSteps.GetAllPipelineTokens();
            ctx.Output.RecordInit("Pipeline Tokens", true, $"{AllPipelineTokens.Count} tokens");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  \u26a0 Pipeline tokens: {ex.Message}");
        }

        MarkInitialized();
    }

    public async ValueTask DisposeAsync()
    {
        if (PlaywrightTool != null)
            await PlaywrightTool.DisposeAsync();

        IsInitialized = false;
    }
}
