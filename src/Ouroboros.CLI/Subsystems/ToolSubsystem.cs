// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text.RegularExpressions;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools.MeTTa;

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

    // ═══════════════════════════════════════════════════════════════════════════
    // EXTRACTED TOOL METHODS — migrated from OuroborosAgent
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pre-emptively executes tools based on input pattern matching (PTZ, camera, smart home, code search).
    /// Returns tool context to inject into the LLM prompt so it uses real data.
    /// </summary>
    internal async Task<string> TryAutoToolExecution(string input)
    {
        var results = new List<string>();
        var inputLower = input.ToLowerInvariant();

        // PTZ movement requests
        var ptzMatch = Regex.Match(inputLower,
            @"\b(pan\s*(left|right)|tilt\s*(up|down)|turn.*camera.*(left|right|up|down)|rotate.*camera|move.*camera.*(left|right|up|down)|point.*camera|camera.*(left|right|up|down)|look\s*(left|right|up|down)|patrol|sweep|go\s*home|center\s*camera)\b");
        if (ptzMatch.Success)
        {
            var ptzTool = Tools.All.FirstOrDefault(t => t.Name == "camera_ptz");
            if (ptzTool != null)
            {
                try
                {
                    var matchText = ptzMatch.Value.ToLowerInvariant();
                    var ptzCommand = matchText switch
                    {
                        var m when m.Contains("left") => "pan_left",
                        var m when m.Contains("right") => "pan_right",
                        var m when m.Contains("up") => "tilt_up",
                        var m when m.Contains("down") => "tilt_down",
                        var m when m.Contains("home") || m.Contains("center") => "go_home",
                        var m when m.Contains("patrol") || m.Contains("sweep") => "patrol",
                        _ => "stop"
                    };

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"[Auto-Tool] PTZ: {ptzCommand}");
                    Console.ResetColor();

                    var ptzResult = await ptzTool.InvokeAsync(ptzCommand, CancellationToken.None);
                    var ptzOutput = ptzResult.Match(ok => ok, err => $"[PTZ error: {err}]");
                    return $"PTZ MOVEMENT RESULT:\n{ptzOutput}\n\nReport the camera movement result to the user. If it succeeded, let them know. If it failed, explain the error honestly.";
                }
                catch (Exception ex)
                {
                    return $"PTZ MOVEMENT ATTEMPTED BUT FAILED:\n{ex.Message}\n\nReport this error honestly to the user.";
                }
            }
            else
            {
                return "PTZ STATUS: The camera_ptz tool is not available. Camera PTZ hardware may not be configured. Report this honestly.";
            }
        }

        // Camera/vision requests
        if (Regex.IsMatch(inputLower,
            @"\b(camera|cam|visual|snapshot|what do you see|look around|check.*room|see.*through)\b"))
        {
            var cameraTool = Tools.All.FirstOrDefault(t => t.Name == "capture_camera");
            if (cameraTool != null)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("[Auto-Tool] Capturing camera frame...");
                    Console.ResetColor();

                    var captureResult = await cameraTool.InvokeAsync("", CancellationToken.None);
                    var captureOutput = captureResult.Match(ok => ok, err => $"[Camera error: {err}]");
                    return $"LIVE CAMERA FEED:\n{captureOutput}\n\nDescribe what the camera captured above. Do NOT make up or hallucinate any visual details - only report what appears in the camera output. If it shows an error, explain the error honestly.";
                }
                catch (Exception ex)
                {
                    return $"CAMERA CAPTURE ATTEMPTED BUT FAILED:\n{ex.Message}\n\nReport this error honestly to the user. Do NOT make up or hallucinate any visual details.";
                }
            }
            else
            {
                return "CAMERA STATUS: The capture_camera tool is not available. Camera hardware may not be configured. Report this honestly - do NOT hallucinate or make up what you see through a camera.";
            }
        }

        // Smart home requests
        var smartHomeMatch = Regex.Match(inputLower,
            @"\b(turn\s*(on|off)|switch\s*(on|off)|light\s*(on|off)|plug\s*(on|off)|set\s*(color|brightness|colour)|list\s*devices?|device\s*info)\b");
        if (smartHomeMatch.Success)
        {
            var smartHomeTool = Tools.All.FirstOrDefault(t => t.Name == "smart_home");
            if (smartHomeTool != null)
            {
                try
                {
                    var smartCommand = ParseSmartHomeCommand(inputLower);

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"[Auto-Tool] Smart Home: {smartCommand}");
                    Console.ResetColor();

                    var smartResult = await smartHomeTool.InvokeAsync(smartCommand, CancellationToken.None);
                    var smartOutput = smartResult.Match(ok => ok, err => $"[Smart home error: {err}]");
                    return $"SMART HOME RESULT:\n{smartOutput}\n\nReport the smart home action result to the user.";
                }
                catch (Exception ex)
                {
                    return $"SMART HOME ATTEMPTED BUT FAILED:\n{ex.Message}\n\nReport this error honestly to the user.";
                }
            }
            else
            {
                return "SMART HOME STATUS: The smart_home tool is not available. Tapo REST API server may not be configured. " +
                       "Set Tapo:ServerAddress in appsettings.json and ensure tapo-rest server is running.";
            }
        }

        // Pattern matching for knowledge-seeking questions about code/architecture
        var codePatterns = new[]
        {
            ("world model", "WorldModel"),
            ("worldmodel", "WorldModel"),
            ("introspection", "Introspection"),
            ("memory", "MemoryIntegration OR TrackedVectorStore"),
            ("tool system", "ITool OR ToolRegistry"),
            ("architecture", "OuroborosAgent"),
            ("how does", inputLower.Replace("how does ", "").Replace(" work", "").Trim()),
            ("is there a", inputLower.Replace("is there a ", "").Replace("?", "").Trim()),
            ("do we have", inputLower.Replace("do we have ", "").Replace("?", "").Trim()),
            ("what is the", inputLower.Replace("what is the ", "").Replace("?", "").Trim()),
            ("show me", inputLower.Replace("show me ", "").Replace("the ", "").Trim()),
            ("where is", inputLower.Replace("where is ", "").Replace("?", "").Trim()),
            ("find", inputLower.Replace("find ", "").Trim()),
        };

        string? searchTerm = null;
        foreach (var (pattern, term) in codePatterns)
        {
            if (inputLower.Contains(pattern))
            {
                searchTerm = term;
                break;
            }
        }

        if (inputLower.Contains("upgrade") || inputLower.Contains("what changed") || inputLower.Contains("recent changes"))
        {
            searchTerm = "// TODO OR // HACK OR DateTime.Now";
        }

        if (string.IsNullOrEmpty(searchTerm))
            return string.Empty;

        // Execute search_my_code tool automatically
        var searchTool = Tools.All.FirstOrDefault(t => t.Name == "search_my_code");
        if (searchTool != null)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"[Auto-Tool] Searching codebase for: {searchTerm}");
                Console.ResetColor();

                var searchResultResult = await searchTool.InvokeAsync(searchTerm, CancellationToken.None);
                var searchResult = searchResultResult.Match(ok => ok, err => null);
                if (!string.IsNullOrEmpty(searchResult))
                {
                    results.Add($"Search results for '{searchTerm}':\n{searchResult}");

                    var fileMatch = Regex.Match(searchResult, @"([\w/\\]+\.cs)");
                    if (fileMatch.Success)
                    {
                        var readTool = Tools.All.FirstOrDefault(t => t.Name == "read_my_file");
                        if (readTool != null)
                        {
                            try
                            {
                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                Console.WriteLine($"[Auto-Tool] Reading file: {fileMatch.Value}");
                                Console.ResetColor();

                                var fileContentResult = await readTool.InvokeAsync(fileMatch.Value, CancellationToken.None);
                                var fileContent = fileContentResult.Match(ok => ok, err => null);
                                if (!string.IsNullOrEmpty(fileContent) && fileContent.Length < 5000)
                                {
                                    results.Add($"File content ({fileMatch.Value}):\n{fileContent}");
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Auto-Tool] Error: {ex.Message}");
            }
        }

        return string.Join("\n\n", results);
    }

    /// <summary>
    /// Executes tools directly based on thought content patterns.
    /// The LLM ignores [TOOL:...] syntax, so direct invocation always works.
    /// </summary>
    internal async Task ExecuteToolsFromThought(string thought)
    {
        var thoughtLower = thought.ToLowerInvariant();

        try
        {
            // Pattern: "search for X" / "find X" / "look up X" in code
            if (thoughtLower.Contains("search") || thoughtLower.Contains("find") || thoughtLower.Contains("look"))
            {
                var searchTarget = ExtractSearchTarget(thought);
                if (!string.IsNullOrEmpty(searchTarget))
                {
                    var searchTool = Tools.All.FirstOrDefault(t => t.Name == "search_my_code");
                    if (searchTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[Thought→Action] Searching: {searchTarget}");
                        Console.ResetColor();

                        var result = await searchTool.InvokeAsync(searchTarget, CancellationToken.None);
                        var content = result.Match(ok => ok, err => null);
                        if (!string.IsNullOrEmpty(content))
                        {
                            Memory.LastThoughtContent = $"Search results for '{searchTarget}': {CognitiveSubsystem.TruncateText(content, 500)}";
                        }
                    }
                }
            }

            // Pattern: "read file X" / "check file X" / "look at X.cs"
            if (thoughtLower.Contains("read") || thoughtLower.Contains("check") || thoughtLower.Contains("look at"))
            {
                var fileMatch = Regex.Match(thought, @"([\w/\\]+\.(?:cs|json|md|txt|yaml|yml))", RegexOptions.IgnoreCase);
                if (fileMatch.Success)
                {
                    var readTool = Tools.All.FirstOrDefault(t => t.Name == "read_my_file");
                    if (readTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[Thought→Action] Reading: {fileMatch.Value}");
                        Console.ResetColor();

                        var result = await readTool.InvokeAsync(fileMatch.Value, CancellationToken.None);
                        var content = result.Match(ok => ok, err => null);
                        if (!string.IsNullOrEmpty(content))
                        {
                            Memory.LastThoughtContent = $"File content ({fileMatch.Value}): {CognitiveSubsystem.TruncateText(content, 500)}";
                        }
                    }
                }
            }

            // Pattern: "calculate X" / "compute X" / math expressions
            if (thoughtLower.Contains("calculate") || thoughtLower.Contains("compute") || Regex.IsMatch(thought, @"\d+\s*[+\-*/]\s*\d+"))
            {
                var mathMatch = Regex.Match(thought, @"[\d\s+\-*/().]+");
                if (mathMatch.Success && mathMatch.Value.Trim().Length > 2)
                {
                    var calcTool = Tools.All.FirstOrDefault(t => t.Name == "calculator");
                    if (calcTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[Thought→Action] Calculating: {mathMatch.Value.Trim()}");
                        Console.ResetColor();

                        var result = await calcTool.InvokeAsync(mathMatch.Value.Trim(), CancellationToken.None);
                        var content = result.Match(ok => ok, err => null);
                        if (!string.IsNullOrEmpty(content))
                        {
                            Memory.LastThoughtContent = $"Calculation result: {content}";
                        }
                    }
                }
            }

            // Pattern: "fetch URL" / "get page" / URLs in thought
            var urlMatch = Regex.Match(thought, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                var fetchTool = Tools.All.FirstOrDefault(t => t.Name == "fetch_url");
                if (fetchTool != null)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Thought→Action] Fetching: {urlMatch.Value}");
                    Console.ResetColor();

                    var result = await fetchTool.InvokeAsync(urlMatch.Value, CancellationToken.None);
                    var content = result.Match(ok => ok, err => null);
                    if (!string.IsNullOrEmpty(content))
                    {
                        Memory.LastThoughtContent = $"Fetched content from {urlMatch.Value}: {CognitiveSubsystem.TruncateText(content, 500)}";
                    }
                }
            }

            // Pattern: "web search" / "research online" / "look up online"
            if (thoughtLower.Contains("web search") || thoughtLower.Contains("research online") ||
                thoughtLower.Contains("search online") || thoughtLower.Contains("look up online"))
            {
                var searchTarget = ExtractSearchTarget(thought);
                if (!string.IsNullOrEmpty(searchTarget))
                {
                    var webTool = Tools.All.FirstOrDefault(t => t.Name == "web_research")
                               ?? Tools.All.FirstOrDefault(t => t.Name == "duckduckgo_search");
                    if (webTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"[Thought→Action] Web research: {searchTarget}");
                        Console.ResetColor();

                        var result = await webTool.InvokeAsync(searchTarget, CancellationToken.None);
                        var content = result.Match(ok => ok, err => null);
                        if (!string.IsNullOrEmpty(content))
                        {
                            Memory.LastThoughtContent = $"Web research results for '{searchTarget}': {CognitiveSubsystem.TruncateText(content, 500)}";
                        }
                    }
                }
            }

            // Pattern: mentions "qdrant" / "memory" / "vector store"
            if (thoughtLower.Contains("qdrant") || thoughtLower.Contains("memory status") || thoughtLower.Contains("vector store"))
            {
                var qdrantTool = Tools.All.FirstOrDefault(t => t.Name == "qdrant_admin");
                if (qdrantTool != null)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Thought→Action] Checking Qdrant status");
                    Console.ResetColor();

                    var result = await qdrantTool.InvokeAsync("{\"command\":\"status\"}", CancellationToken.None);
                    var content = result.Match(ok => ok, err => null);
                    if (!string.IsNullOrEmpty(content))
                    {
                        Memory.LastThoughtContent = $"Qdrant status: {CognitiveSubsystem.TruncateText(content, 500)}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Thought→Action] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the search target from a thought containing search-related phrases.
    /// </summary>
    internal static string ExtractSearchTarget(string thought)
    {
        var quotedMatch = Regex.Match(thought, @"[""']([^""']+)[""']");
        if (quotedMatch.Success)
            return quotedMatch.Groups[1].Value;

        var patterns = new[]
        {
            @"search(?:\s+for)?\s+(.+?)(?:\s+in|\s+to|\.|$)",
            @"find\s+(.+?)(?:\s+in|\s+to|\.|$)",
            @"look(?:\s+up)?\s+(.+?)(?:\s+in|\s+to|\.|$)",
            @"check\s+(.+?)(?:\s+in|\s+to|\.|$)",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(thought, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Value.Length > 2)
            {
                var target = match.Groups[1].Value.Trim();
                target = Regex.Replace(target, @"^(the|a|an|my|our)\s+", "", RegexOptions.IgnoreCase);
                if (target.Length > 2)
                    return target;
            }
        }

        var words = thought.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !IsCommonWord(w))
            .Take(3);
        return string.Join(" ", words);
    }

    internal static bool IsCommonWord(string word)
    {
        var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "that", "this", "with", "from", "have", "been",
            "will", "would", "could", "should", "about", "into", "than", "then",
            "there", "their", "they", "what", "when", "where", "which", "while",
            "being", "these", "those", "some", "such", "only", "also", "just",
            "search", "find", "look", "check", "need", "want", "think", "know"
        };
        return common.Contains(word);
    }

    /// <summary>
    /// Post-processes LLM response to execute tools when LLM talks about using them but doesn't.
    /// </summary>
    internal async Task<(string EnhancedResponse, List<ToolExecution> ExecutedTools)> PostProcessResponseForTools(string response, string originalInput)
    {
        var executedTools = new List<ToolExecution>();
        var responseLower = response.ToLowerInvariant();
        var enhancedParts = new List<string>();
        bool needsEnhancement = false;

        try
        {
            bool claimsSearch = responseLower.Contains("i searched") ||
                               responseLower.Contains("searching") ||
                               responseLower.Contains("looked through") ||
                               responseLower.Contains("checking the code") ||
                               responseLower.Contains("looking at the") ||
                               responseLower.Contains("i found") ||
                               responseLower.Contains("when i searched") ||
                               responseLower.Contains("i checked") ||
                               responseLower.Contains("i looked") ||
                               responseLower.Contains("found references") ||
                               responseLower.Contains("found some") ||
                               responseLower.Contains("found the") ||
                               responseLower.Contains("search showed") ||
                               responseLower.Contains("examining") ||
                               responseLower.Contains("looking for") ||
                               responseLower.Contains("tried to find") ||
                               responseLower.Contains("no direct matches") ||
                               responseLower.Contains("couldn't find") ||
                               responseLower.Contains("doesn't exist") ||
                               responseLower.Contains("isn't where") ||
                               responseLower.Contains("file path") ||
                               responseLower.Contains("looking at") ||
                               (responseLower.Contains("found") && responseLower.Contains("codebase"));

            if (claimsSearch && !responseLower.Contains("[tool:"))
            {
                var searchTarget = ExtractClaimedSearchTarget(response, originalInput);
                if (!string.IsNullOrEmpty(searchTarget))
                {
                    var searchTool = Tools.All.FirstOrDefault(t => t.Name == "search_my_code");
                    if (searchTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[Post-Process] LLM claimed to search - actually searching: {searchTarget}");
                        Console.ResetColor();

                        var result = await searchTool.InvokeAsync(searchTarget, CancellationToken.None);
                        var content = result.Match(ok => ok, err => $"Error: {err}");

                        executedTools.Add(new ToolExecution("search_my_code", searchTarget, content, DateTime.UtcNow));
                        enhancedParts.Add($"\n\n📎 **Actual search results for '{searchTarget}':**\n{CognitiveSubsystem.TruncateText(content, 1000)}");
                        needsEnhancement = true;
                    }
                }
            }

            // Pattern: LLM says "reading the file" but didn't call tool
            if ((responseLower.Contains("reading") || responseLower.Contains("looking at") ||
                 responseLower.Contains("checking file") || responseLower.Contains("in the file")) &&
                !responseLower.Contains("[tool:"))
            {
                var fileMatch = Regex.Match(response, @"([\w/\\]+\.(?:cs|json|md|txt|yaml|yml))", RegexOptions.IgnoreCase);
                if (fileMatch.Success)
                {
                    var readTool = Tools.All.FirstOrDefault(t => t.Name == "read_my_file");
                    if (readTool != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[Post-Process] LLM mentioned file - actually reading: {fileMatch.Value}");
                        Console.ResetColor();

                        var result = await readTool.InvokeAsync(fileMatch.Value, CancellationToken.None);
                        var content = result.Match(ok => ok, err => $"Error: {err}");

                        if (!content.StartsWith("Error"))
                        {
                            executedTools.Add(new ToolExecution("read_my_file", fileMatch.Value, content, DateTime.UtcNow));
                            enhancedParts.Add($"\n\n📄 **Actual file content ({fileMatch.Value}):**\n```\n{CognitiveSubsystem.TruncateText(content, 800)}\n```");
                            needsEnhancement = true;
                        }
                    }
                }
            }

            // Pattern: LLM talks about calculations
            var mathMatch = Regex.Match(response, @"(\d+(?:\.\d+)?)\s*([+\-*/])\s*(\d+(?:\.\d+)?)");
            if (mathMatch.Success && responseLower.Contains("calculat"))
            {
                var calcTool = Tools.All.FirstOrDefault(t => t.Name == "calculator");
                if (calcTool != null)
                {
                    var expr = mathMatch.Value;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Post-Process] LLM mentioned calculation - actually calculating: {expr}");
                    Console.ResetColor();

                    var result = await calcTool.InvokeAsync(expr, CancellationToken.None);
                    var content = result.Match(ok => ok, err => $"Error: {err}");

                    executedTools.Add(new ToolExecution("calculator", expr, content, DateTime.UtcNow));
                    enhancedParts.Add($"\n\n🔢 **Calculation result:** {expr} = {content}");
                    needsEnhancement = true;
                }
            }

            // Pattern: LLM mentions URLs but didn't fetch
            var urlMatch = Regex.Match(response, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);
            if (urlMatch.Success && (responseLower.Contains("fetch") || responseLower.Contains("check") ||
                                      responseLower.Contains("visit") || responseLower.Contains("see")))
            {
                var fetchTool = Tools.All.FirstOrDefault(t => t.Name == "fetch_url");
                if (fetchTool != null && !urlMatch.Value.Contains("example.com"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Post-Process] LLM mentioned URL - actually fetching: {urlMatch.Value}");
                    Console.ResetColor();

                    var result = await fetchTool.InvokeAsync(urlMatch.Value, CancellationToken.None);
                    var content = result.Match(ok => ok, err => $"Error: {err}");

                    if (!content.StartsWith("Error"))
                    {
                        executedTools.Add(new ToolExecution("fetch_url", urlMatch.Value, content, DateTime.UtcNow));
                        enhancedParts.Add($"\n\n🌐 **Fetched content from {urlMatch.Value}:**\n{CognitiveSubsystem.TruncateText(content, 500)}");
                        needsEnhancement = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Post-Process] Error: {ex.Message}");
        }

        if (needsEnhancement)
        {
            return (response + string.Join("", enhancedParts), executedTools);
        }

        return (response, executedTools);
    }

    /// <summary>
    /// Extracts what the LLM claims to have searched for based on context.
    /// </summary>
    internal static string ExtractClaimedSearchTarget(string response, string originalInput)
    {
        var quotedMatch = Regex.Match(response, @"[""']([^""']+)[""']");
        if (quotedMatch.Success && quotedMatch.Groups[1].Value.Length > 2 && quotedMatch.Groups[1].Value.Length < 50)
            return quotedMatch.Groups[1].Value;

        var fileClassMatch = Regex.Match(response, @"(\b[A-Z][a-zA-Z]+(?:Command|Manager|Service|Agent|Config|Tool|Engine)(?:\.cs)?)\b");
        if (fileClassMatch.Success)
            return fileClassMatch.Groups[1].Value.Replace(".cs", "");

        var hyphenMatch = Regex.Match(response, @"\b([a-z]+-[a-z]+(?:-[a-z]+)?)\b", RegexOptions.IgnoreCase);
        if (hyphenMatch.Success && hyphenMatch.Groups[1].Value.Length > 4)
            return hyphenMatch.Groups[1].Value;

        var patterns = new[]
        {
            @"search(?:ed|ing)?\s+(?:for\s+)?[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"look(?:ed|ing)?\s+(?:for|at|through)\s+[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"found\s+(?:references?\s+to\s+)?[""']?(.+?)[""']?(?:\s+in|\s+that|\s+scattered|\.|,|$)",
            @"check(?:ed|ing)?\s+(?:for\s+)?[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"the\s+(?:actual\s+)?(\w+(?:Command|Manager|Config|\.cs))",
            @"(\w+\.cs)\s+(?:file|doesn't|isn't|was)",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(response, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Value.Length > 2 && match.Groups[1].Value.Length < 50)
            {
                var target = match.Groups[1].Value.Trim();
                target = Regex.Replace(target, @"^(the|a|an|my|your|our|some|any)\s+", "", RegexOptions.IgnoreCase);
                target = target.TrimEnd('.', ',', '!', '?');
                if (target.Length > 2 && !target.Contains(" there ") && !target.Contains(" was "))
                    return target;
            }
        }

        var inputLower = originalInput.ToLowerInvariant();
        if (inputLower.Contains("world model")) return "WorldModel";
        if (inputLower.Contains("sub-agent") || inputLower.Contains("subagent")) return "SubAgent";
        if (inputLower.Contains("qwen")) return "qwen";
        if (inputLower.Contains("model")) return "ModelConfig OR ModelsCommand";
        if (inputLower.Contains("tool")) return "ITool";
        if (inputLower.Contains("memory")) return "MemoryIntegration";
        if (inputLower.Contains("architecture")) return "OuroborosAgent";
        if (inputLower.Contains("troubleshoot")) return "error OR exception";

        var words = originalInput.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4 && char.IsLetter(w[0]))
            .Take(2);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Detects and corrects LLM misinformation about tool availability.
    /// </summary>
    internal static string DetectAndCorrectToolMisinformation(string response)
    {
        string[] falseClaimPatterns =
        [
            "tools aren't responding", "tool.*not.*available", "tool.*offline",
            "tool.*unavailable", "file.*tools.*issues", "can't access.*tools",
            "tools.*playing hide", "tools.*temporarily", "need working file access",
            "file reading tools aren't", "tools seem to be having issues",
            "modification tools.*offline", "self-modification.*offline",
            "permissions snags", "being finicky", "access is being finicky",
            "hitting.*snags", "code access.*finicky", "search.*hitting.*snag",
            "direct.*access.*problem", "file access.*issue", "can't.*read.*code",
            "unable to access.*code", "code.*not accessible", "tools.*not working",
            "search.*not.*working", "having trouble.*access", "trouble accessing",
            "access.*trouble", "can't seem to", "seems? to be blocked", "blocked by",
            "not able to.*file", "unable to.*file", "file system.*issue",
            "filesystem.*issue", "need you to.*manually", "you'll need to.*yourself",
            "could you.*instead", "would you mind.*manually", "connectivity issues",
            "connection issue", "tools.*connectivity", "internal tools.*issue",
            "tools.*having.*issue", "frustrating.*tools", "try a different approach",
            "error with the.*tool", "getting an error", "search tool.*error"
        ];

        bool llmClaimingToolsUnavailable = falseClaimPatterns.Any(pattern =>
            Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase));

        if (llmClaimingToolsUnavailable)
        {
            response += @"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️ **Note from System**: The model above may be mistaken about tool availability.

**Direct commands you can use RIGHT NOW:**
• `save {""file"":""path.cs"",""search"":""old"",""replace"":""new""}` - Modify code
• `/read path/to/file.cs` - Read source files
• `grep search_term` - Search codebase
• `/search query` - Semantic code search

Example: `save src/Ouroboros.CLI/Commands/OuroborosAgent.cs ""old code"" ""new code""`
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        return response;
    }

    /// <summary>
    /// Parses natural language smart home command into tool input format.
    /// </summary>
    internal static string ParseSmartHomeCommand(string input)
    {
        if (input.Contains("list") && input.Contains("device"))
            return "list_devices";

        string action;
        if (input.Contains("set") && (input.Contains("color") || input.Contains("colour")))
            action = "set_color";
        else if (input.Contains("set") && input.Contains("bright"))
            action = "set_brightness";
        else if (input.Contains("device") && input.Contains("info"))
            action = "device_info";
        else if (input.Contains("turn") && input.Contains("off") || input.Contains("switch") && input.Contains("off"))
            action = "turn_off";
        else
            action = "turn_on";

        var deviceName = ExtractDeviceName(input);
        return $"{action} {deviceName}";
    }

    /// <summary>
    /// Extracts a device name from natural language input.
    /// </summary>
    internal static string ExtractDeviceName(string input)
    {
        var quoteMatch = Regex.Match(input, @"[""']([^""']+)[""']");
        if (quoteMatch.Success)
            return quoteMatch.Groups[1].Value.Trim();

        var afterAction = Regex.Match(input,
            @"\b(?:turn\s*(?:on|off)|switch\s*(?:on|off))\s+(?:the\s+)?(.+?)(?:\s+(?:light|lamp|plug|switch|bulb|strip))?$");
        if (afterAction.Success)
        {
            var raw = afterAction.Groups[1].Value.Trim();
            raw = Regex.Replace(raw, @"\s*(please|now|for me)\s*$", "").Trim();
            if (!string.IsNullOrEmpty(raw))
                return raw;
        }

        return input;
    }

    public async ValueTask DisposeAsync()
    {
        if (PlaywrightTool != null)
            await PlaywrightTool.DisposeAsync();

        IsInitialized = false;
    }
}
