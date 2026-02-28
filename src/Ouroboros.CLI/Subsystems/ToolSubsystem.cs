// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Services;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.Configuration;
using Spectre.Console;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools.MeTTa;
using Qdrant.Client;

/// <summary>
/// Tool subsystem implementation owning tool lifecycle and browser automation.
/// </summary>
public sealed partial class ToolSubsystem : IToolSubsystem
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

    //  Runtime cross-subsystem references  
    internal OuroborosConfig Config { get; private set; } = null!;
    internal IConsoleOutput Output { get; private set; } = null!;
    internal IModelSubsystem Models { get; private set; } = null!;
    internal IMemorySubsystem Memory { get; private set; } = null!;

    public void MarkInitialized() => IsInitialized = true;

    /// <summary>Tool-aware LLM wrapping the effective chat model with all registered tools.</summary>
    public ToolAwareChatModel? Llm { get; set; }

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        Ctx = ctx;
        Config = ctx.Config;
        Output = ctx.Output;
        Models = ctx.Models;
        Memory = ctx.Memory;
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
                {
                    var qdrantSettings = ctx.Services?.GetService<QdrantSettings>();
                    var qdrantRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
                    Func<string, CancellationToken, Task<float[]>>? embedFunc = null;
                    if (ctx.Models.Embedding != null)
                        embedFunc = async (text, ct) => await ctx.Models.Embedding.CreateEmbeddingsAsync(text, ct);
                    if (qdrantSettings != null && qdrantRegistry != null)
                    {
                        var qdrantAdmin = new QdrantAdminTool(qdrantSettings, qdrantRegistry, embedFunc);
                        Tools = Tools.WithTool(qdrantAdmin);
                        ctx.Output.RecordInit("Qdrant Admin", true, "self-management tool (DI)");
                    }
                    else if (!string.IsNullOrEmpty(ctx.Config.QdrantEndpoint))
                    {
                        var qdrantRest = ctx.Config.QdrantEndpoint.Replace(":6334", ":6333");
                        var qdrantAdmin = new QdrantAdminTool(qdrantRest, embedFunc);
                        Tools = Tools.WithTool(qdrantAdmin);
                        ctx.Output.RecordInit("Qdrant Admin", true, "self-management tool");
                    }
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
                    catch (InvalidOperationException ex)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Playwright: Not available ({Markup.Escape(ex.Message)})"));
                    }
                    catch (IOException ex)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Playwright: Not available ({Markup.Escape(ex.Message)})"));
                    }
                }
                else
                {
                    ctx.Output.RecordInit("Playwright", false, "disabled");
                }

                // Register camera capture tool (cross-cutting - provided by agent)
                try { ctx.RegisterCameraCaptureAction?.Invoke(); }
                catch (InvalidOperationException ex) { System.Diagnostics.Debug.WriteLine($"[Tools] capture_camera registration failed: {ex.Message}"); }

                // Create final ToolAwareChatModel with ALL tools
                var effectiveModel = ctx.Models.OrchestratedModel as Ouroboros.Abstractions.Core.IChatCompletionModel ?? chatModel;
                Llm = new ToolAwareChatModel(effectiveModel, Tools);
                ToolFactory = new DynamicToolFactory(Llm);

                // Tool Learner (needs embedding + MeTTa)
                if (ctx.Models.Embedding != null)
                {
                    var mettaEngine = new InMemoryMeTTaEngine();
                    ctx.Memory.MeTTaEngine = mettaEngine; // Set MeTTa on Memory subsystem

                    var tlClient = ctx.Services?.GetService<QdrantClient>();
                    var tlRegistry = ctx.Services?.GetService<IQdrantCollectionRegistry>();
                    if (tlClient != null && tlRegistry != null)
                        ToolLearner = new IntelligentToolLearner(
                            ToolFactory, mettaEngine, ctx.Models.Embedding, Llm, tlClient, tlRegistry);
                    else
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

                // OpenClaw Gateway integration
                if (ctx.Config.EnableOpenClaw)
                {
                    try
                    {
                        var gw = await OpenClawTools.ConnectGatewayAsync(
                            ctx.Config.OpenClawGateway,
                            ctx.Config.OpenClawToken);
                        Tools = Tools.WithOpenClawTools();
                        ctx.Output.RecordInit("OpenClaw", true,
                            $"gateway {gw} ({OpenClawTools.GetAllTools().Count()} tools)");
                    }
                    catch (HttpRequestException ex)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                            $"  [!] OpenClaw: {Markup.Escape(ex.Message)}"));
                        ctx.Output.RecordInit("OpenClaw", false, ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                            $"  [!] OpenClaw: {Markup.Escape(ex.Message)}"));
                        ctx.Output.RecordInit("OpenClaw", false, ex.Message);
                    }

                    // PC Node (register this machine as a device node)
                    if (ctx.Config.EnablePcNode)
                    {
                        try
                        {
                            var pcConfig = !string.IsNullOrEmpty(ctx.Config.PcNodeConfigPath)
                                ? PcNodeSecurityConfig.CreateFromFile(ctx.Config.PcNodeConfigPath)
                                : (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development"
                                    ? PcNodeSecurityConfig.CreateDevelopment()
                                    : PcNodeSecurityConfig.CreateDefault());

                            var deviceIdentity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
                            await OpenClawPcNodeTools.StartPcNodeAsync(
                                pcConfig,
                                ctx.Config.OpenClawGateway,
                                ctx.Config.OpenClawToken,
                                deviceIdentity);

                            Tools = Tools.WithPcNodeTools();

                            var enabledCount = pcConfig.EnabledCapabilities.Count;
                            ctx.Output.RecordInit("PcNode", true,
                                $"{enabledCount} capabilities enabled ({OpenClawPcNodeTools.GetAllTools().Count()} tools)");
                        }
                        catch (HttpRequestException ex)
                        {
                            AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                                $"  [!] PC Node: {Markup.Escape(ex.Message)}"));
                            ctx.Output.RecordInit("PcNode", false, ex.Message);
                        }
                        catch (InvalidOperationException ex)
                        {
                            AnsiConsole.MarkupLine(OuroborosTheme.Warn(
                                $"  [!] PC Node: {Markup.Escape(ex.Message)}"));
                            ctx.Output.RecordInit("PcNode", false, ex.Message);
                        }
                    }
                }
                else
                {
                    ctx.Output.RecordInit("OpenClaw", false, "disabled");
                }

                // CRITICAL: Recreate Llm with ALL tools (immutable ToolRegistry)
                Llm = new ToolAwareChatModel(effectiveModel, Tools);
                System.Diagnostics.Debug.WriteLine($"[Tools] Final Llm created with {Tools.Count} tools");

                // Wire Crush-style permission + event hooks onto the final LLM instance
                WireHooks(Llm);
            }
            else
            {
                ctx.Output.RecordInit("Tools", true, $"{Tools.Count} (static only)");
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Tool factory failed: {Markup.Escape(ex.Message)}"));
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Tool factory failed: {Markup.Escape(ex.Message)}"));
        }

        // ── Pipeline DSL tokens ──
        try
        {
            AllPipelineTokens = SkillCliSteps.GetAllPipelineTokens();
            ctx.Output.RecordInit("Pipeline Tokens", true, $"{AllPipelineTokens.Count} tokens");
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Pipeline tokens: {Markup.Escape(ex.Message)}"));
        }

        MarkInitialized();
    }

    public async ValueTask DisposeAsync()
    {
        if (PlaywrightTool != null)
            await PlaywrightTool.DisposeAsync();

        if (OpenClawPcNodeTools.SharedPcNode != null)
            await OpenClawPcNodeTools.SharedPcNode.DisposeAsync();

        if (OpenClawTools.SharedClient != null)
            await OpenClawTools.SharedClient.DisposeAsync();

        IsInitialized = false;
    }
}
