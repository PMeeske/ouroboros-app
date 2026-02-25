// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Application;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain;

/// <summary>
/// Command routing subsystem: parses raw user input into typed action triples
/// and provides system-level informational responses (help, status, mood, DSL tokens).
/// </summary>
public sealed class CommandRoutingSubsystem : ICommandRoutingSubsystem
{
    public string Name => "CommandRouting";
    public bool IsInitialized { get; private set; }

    private OuroborosConfig _config = null!;
    private IConsoleOutput _output = null!;
    private VoiceSubsystem _voiceSub = null!;
    private ModelSubsystem _modelsSub = null!;
    private ToolSubsystem _toolsSub = null!;
    private MemorySubsystem _memorySub = null!;
    private AutonomySubsystem _autonomySub = null!;

    public Task InitializeAsync(SubsystemInitContext ctx)
    {
        _config = ctx.Config;
        _output = ctx.Output;
        _voiceSub = ctx.Voice;
        _modelsSub = ctx.Models;
        _toolsSub = ctx.Tools;
        _memorySub = ctx.Memory;
        _autonomySub = ctx.Autonomy;
        IsInitialized = true;
        ctx.Output.RecordInit("CommandRouting", true, "routing ready");
        return Task.CompletedTask;
    }

    // ── Routing ─────────────────────────────────────────────────────────────

    public (ActionType Type, string Argument, string? ToolInput) ParseAction(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        // Thought input prefixed with [💭] — track, auto-execute tools, acknowledge
        if (input.TrimStart().StartsWith("[💭]"))
        {
            var thought = input.TrimStart()[4..].Trim();
            _memorySub.TrackLastThought(thought);
            _ = Task.Run(async () => await _toolsSub.ExecuteToolsFromThought(thought));
            return (ActionType.SaveThought, thought, null);
        }

        // Help
        if (lower is "help" or "?" or "commands")
            return (ActionType.Help, "", null);

        // Status
        if (lower is "status" or "state" or "stats")
            return (ActionType.Status, "", null);

        // Mood
        if (lower.Contains("how are you") || lower.Contains("how do you feel") || lower is "mood")
            return (ActionType.Mood, "", null);

        // List skills
        if (lower.StartsWith("list skill") || lower == "skills" || lower == "what skills")
            return (ActionType.ListSkills, "", null);

        // List tools
        if (lower.StartsWith("list tool") || lower == "tools" || lower == "what tools")
            return (ActionType.ListTools, "", null);

        // Learn
        if (lower.StartsWith("learn about ")) return (ActionType.LearnTopic, input[12..].Trim(), null);
        if (lower.StartsWith("learn ")) return (ActionType.LearnTopic, input[6..].Trim(), null);
        if (lower.StartsWith("research ")) return (ActionType.LearnTopic, input[9..].Trim(), null);

        // Tool creation
        if (lower.StartsWith("create tool ") || lower.StartsWith("add tool "))
            return (ActionType.CreateTool, input.Split(' ', 3).Last(), null);
        if (lower.StartsWith("make a ") && lower.Contains("tool"))
            return (ActionType.CreateTool, ExtractToolName(input), null);

        // Tool usage
        if (lower.StartsWith("use ") && lower.Contains(" to "))
        {
            var parts = input[4..].Split(" to ", 2);
            return (ActionType.UseTool, parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null);
        }
        if (lower.StartsWith("search for ") || lower.StartsWith("search "))
        {
            var query = lower.StartsWith("search for ") ? input[11..] : input[7..];
            return (ActionType.UseTool, "search", query.Trim());
        }

        // Run skill
        if (lower.StartsWith("run ") || lower.StartsWith("execute "))
            return (ActionType.RunSkill, input.Split(' ', 2).Last(), null);

        // Suggest
        if (lower.StartsWith("suggest ")) return (ActionType.Suggest, input[8..].Trim(), null);

        // Plan
        if (lower.StartsWith("plan ") || lower.StartsWith("how would you "))
            return (ActionType.Plan, input.Split(' ', 2).Last(), null);

        // Execute with planning
        if (lower.StartsWith("do ") || lower.StartsWith("accomplish "))
            return (ActionType.Execute, input.Split(' ', 2).Last(), null);

        // Memory
        if (lower.StartsWith("remember ")) return (ActionType.Remember, input[9..].Trim(), null);
        if (lower.StartsWith("recall ") || lower.StartsWith("what do you know about "))
        {
            var topic = lower.StartsWith("recall ") ? input[7..] : input[23..];
            return (ActionType.Recall, topic.Trim(), null);
        }

        // MeTTa query
        if (lower.StartsWith("query ") || lower.StartsWith("metta "))
            return (ActionType.Query, input.Split(' ', 2).Last(), null);

        // Ask - single question mode
        if (lower.StartsWith("ask ")) return (ActionType.Ask, input[4..].Trim(), null);

        // Pipeline - run a DSL pipeline
        if (lower.StartsWith("pipeline ") || lower.StartsWith("pipe "))
        {
            var arg = lower.StartsWith("pipeline ") ? input[9..] : input[5..];
            return (ActionType.Pipeline, arg.Trim(), null);
        }

        // MeTTa direct expression
        if (lower.StartsWith("!(") || lower.StartsWith("(") || lower.StartsWith("metta:"))
        {
            var expr = lower.StartsWith("metta:") ? input[6..] : input;
            return (ActionType.Metta, expr.Trim(), null);
        }

        // Orchestrator mode
        if (lower.StartsWith("orchestrate ") || lower.StartsWith("orch "))
        {
            var arg = lower.StartsWith("orchestrate ") ? input[12..] : input[5..];
            return (ActionType.Orchestrate, arg.Trim(), null);
        }

        // Network commands
        if (lower.StartsWith("network ") || lower == "network")
            return (ActionType.Network, input.Length > 8 ? input[8..].Trim() : "status", null);

        // DAG commands
        if (lower.StartsWith("dag ") || lower == "dag")
            return (ActionType.Dag, input.Length > 4 ? input[4..].Trim() : "show", null);

        // Affect/emotions
        if (lower.StartsWith("affect ") || lower.StartsWith("emotion"))
            return (ActionType.Affect, input.Split(' ', 2).Last(), null);

        // Environment
        if (lower.StartsWith("env ") || lower.StartsWith("environment"))
            return (ActionType.Environment, input.Split(' ', 2).Last(), null);

        // Maintenance
        if (lower.StartsWith("maintenance ") || lower.StartsWith("maintain"))
            return (ActionType.Maintenance, input.Split(' ', 2).Last(), null);

        // Policy
        if (lower.StartsWith("policy ")) return (ActionType.Policy, input[7..].Trim(), null);

        // Explain DSL
        if (lower.StartsWith("explain ")) return (ActionType.Explain, input[8..].Trim(), null);

        // Test
        if (lower.StartsWith("test ") || lower == "test")
            return (ActionType.Test, input.Length > 5 ? input[5..].Trim() : "", null);

        // Consciousness state
        if (lower is "consciousness" or "conscious" or "inner" or "self")
            return (ActionType.Consciousness, "", null);

        // DSL Tokens
        if (lower is "tokens" or "t") return (ActionType.Tokens, "", null);

        // Fetch/learn from arXiv
        if (lower.StartsWith("fetch ")) return (ActionType.Fetch, input[6..].Trim(), null);

        // Process large text with divide-and-conquer
        if (lower.StartsWith("process ") || lower.StartsWith("dc "))
        {
            var arg = lower.StartsWith("process ") ? input[8..].Trim() : input[3..].Trim();
            return (ActionType.Process, arg, null);
        }

        // Self-execution
        if (lower.StartsWith("selfexec ") || lower.StartsWith("self-exec ") || lower == "selfexec")
        {
            var arg = lower.StartsWith("selfexec ") ? input[9..].Trim()
                : lower.StartsWith("self-exec ") ? input[10..].Trim() : "";
            return (ActionType.SelfExec, arg, null);
        }

        // Sub-agent
        if (lower.StartsWith("subagent ") || lower.StartsWith("sub-agent ") || lower == "subagents" || lower == "agents")
        {
            var arg = lower.StartsWith("subagent ") ? input[9..].Trim()
                : lower.StartsWith("sub-agent ") ? input[10..].Trim() : "";
            return (ActionType.SubAgent, arg, null);
        }

        // Epic/project orchestration
        if (lower.StartsWith("epic ") || lower == "epic" || lower == "epics")
        {
            var arg = lower.StartsWith("epic ") ? input[5..].Trim() : "";
            return (ActionType.Epic, arg, null);
        }

        // Goal queue management
        if (lower.StartsWith("goal ") || lower == "goals")
        {
            var arg = lower.StartsWith("goal ") ? input[5..].Trim() : "";
            return (ActionType.Goal, arg, null);
        }

        // Delegate task to sub-agent
        if (lower.StartsWith("delegate ")) return (ActionType.Delegate, input[9..].Trim(), null);

        // Self-model inspection
        if (lower.StartsWith("selfmodel ") || lower.StartsWith("self-model ") || lower == "selfmodel" || lower == "identity")
        {
            var arg = lower.StartsWith("selfmodel ") ? input[10..].Trim()
                : lower.StartsWith("self-model ") ? input[11..].Trim() : "";
            return (ActionType.SelfModel, arg, null);
        }

        // Self-evaluation
        if (lower.StartsWith("evaluate ") || lower == "evaluate" || lower == "assess")
        {
            var arg = lower.StartsWith("evaluate ") ? input[9..].Trim() : "";
            return (ActionType.Evaluate, arg, null);
        }

        // Emergence
        if (lower.StartsWith("emergence ") || lower == "emergence" || lower.StartsWith("emerge "))
        {
            var arg = lower.StartsWith("emergence ") ? input[10..].Trim()
                : lower.StartsWith("emerge ") ? input[7..].Trim() : "";
            return (ActionType.Emergence, arg, null);
        }

        // Dream
        if (lower.StartsWith("dream ") || lower == "dream" || lower.StartsWith("dream about "))
        {
            var arg = lower.StartsWith("dream about ") ? input[12..].Trim()
                : lower.StartsWith("dream ") ? input[6..].Trim() : "";
            return (ActionType.Dream, arg, null);
        }

        // Introspect
        if (lower.StartsWith("introspect ") || lower == "introspect" || lower.Contains("look within"))
        {
            var arg = lower.StartsWith("introspect ") ? input[11..].Trim() : "";
            return (ActionType.Introspect, arg, null);
        }

        // Read my code — direct read_my_file invocation (takes priority over coordinator)
        if (lower.StartsWith("read my code ") || lower.StartsWith("/read ") ||
            lower.StartsWith("show my code ") || lower.StartsWith("cat "))
        {
            var arg = lower.StartsWith("read my code ") ? input[13..].Trim()
                : lower.StartsWith("/read ") ? input[6..].Trim()
                : lower.StartsWith("show my code ") ? input[13..].Trim()
                : input[4..].Trim();
            return (ActionType.ReadMyCode, arg, null);
        }

        // Search my code — direct search_my_code invocation (takes priority over coordinator)
        if (lower.StartsWith("search my code ") || lower.StartsWith("/search ") ||
            lower.StartsWith("grep ") || lower.StartsWith("find in code "))
        {
            var arg = lower.StartsWith("search my code ") ? input[15..].Trim()
                : lower.StartsWith("/search ") ? input[8..].Trim()
                : lower.StartsWith("grep ") ? input[5..].Trim()
                : input[13..].Trim();
            return (ActionType.SearchMyCode, arg, null);
        }

        // Index commands
        if (lower == "reindex" || lower == "reindex full" || lower == "/reindex")
            return (ActionType.Reindex, "", null);
        if (lower == "reindex incremental" || lower == "reindex inc" || lower == "/reindex inc")
            return (ActionType.ReindexIncremental, "", null);
        if (lower.StartsWith("index search ") || lower.StartsWith("/index search "))
        {
            var arg = lower.StartsWith("/index search ") ? input[14..].Trim() : input[13..].Trim();
            return (ActionType.IndexSearch, arg, null);
        }
        if (lower is "index stats" or "/index stats" or "index status")
            return (ActionType.IndexStats, "", null);

        // AGI subsystem commands
        if (lower is "agi status" or "/agi status" or "agi" or "/agi" or "agi stats")
            return (ActionType.AgiStatus, "", null);

        if (lower.StartsWith("council ") || lower.StartsWith("/council ") || lower.StartsWith("debate "))
        {
            var arg = lower.StartsWith("/council ") ? input[9..].Trim() :
                      lower.StartsWith("council ") ? input[8..].Trim() : input[7..].Trim();
            return (ActionType.AgiCouncil, arg, null);
        }

        if (lower is "introspect" or "/introspect" or "agi introspect" or "self analyze" or "self-analyze")
            return (ActionType.AgiIntrospect, "", null);

        if (lower is "world" or "/world" or "world state" or "world model" or "agi world")
            return (ActionType.AgiWorld, "", null);

        if (lower.StartsWith("coordinate ") || lower.StartsWith("/coordinate "))
        {
            var arg = lower.StartsWith("/coordinate ") ? input[12..].Trim() : input[11..].Trim();
            return (ActionType.AgiCoordinate, arg, null);
        }

        if (lower is "experience" or "/experience" or "replay" or "experience buffer" or "agi experience")
            return (ActionType.AgiExperience, "", null);

        if (lower is "prompt" or "/prompt" or "prompt stats" or "prompt optimize" or "prompts")
            return (ActionType.PromptOptimize, "", null);

        // Push mode: route remaining slash commands to coordinator
        if (lower.StartsWith("/") && _autonomySub.Coordinator != null)
            return (ActionType.CoordinatorCommand, input, null);

        // Approve/reject intentions
        if (lower.StartsWith("/approve ") || lower.StartsWith("approve "))
        {
            var arg = lower.StartsWith("/approve ") ? input[9..].Trim() : input[8..].Trim();
            return (ActionType.Approve, arg, null);
        }
        if (lower.StartsWith("/reject ") || lower.StartsWith("reject intention"))
        {
            var arg = lower.StartsWith("/reject ") ? input[8..].Trim() : input[16..].Trim();
            return (ActionType.Reject, arg, null);
        }

        if (lower is "/pending" or "pending" or "pending intentions" or "show intentions")
            return (ActionType.Pending, "", null);
        if (lower is "/pause" or "pause push" or "stop proposing")
            return (ActionType.PushPause, "", null);
        if (lower is "/resume" or "resume push" or "start proposing")
            return (ActionType.PushResume, "", null);

        // Code improvement/analysis detection
        if ((lower.Contains("improve") || lower.Contains("check") || lower.Contains("analyze") ||
             lower.Contains("refactor") || lower.Contains("fix") || lower.Contains("review")) &&
            (lower.Contains(" cs ") || lower.Contains(".cs") || lower.Contains("c# ") ||
             lower.Contains("csharp") || lower.Contains("code") || lower.Contains("file")))
            return (ActionType.AnalyzeCode, input, null);

        // Save thought/learning
        if (lower.StartsWith("save thought ") || lower.StartsWith("/save thought ") ||
            lower.StartsWith("save learning ") || lower.StartsWith("/save learning ") ||
            lower is "save it" or "save thought" or "save learning" or "persist thought")
        {
            var arg = lower.StartsWith("save thought ") ? input[13..].Trim()
                : lower.StartsWith("/save thought ") ? input[14..].Trim()
                : lower.StartsWith("save learning ") ? input[14..].Trim()
                : lower.StartsWith("/save learning ") ? input[15..].Trim()
                : "";
            return (ActionType.SaveThought, arg, null);
        }

        // Save/modify code — direct modify_my_code invocation
        if (lower.StartsWith("save code ") || lower.StartsWith("/save code ") ||
            lower.StartsWith("modify code ") || lower.StartsWith("/modify ") ||
            lower is "save code" or "persist changes" or "write code")
        {
            var arg = lower.StartsWith("save code ") ? input[10..].Trim()
                : lower.StartsWith("/save code ") ? input[11..].Trim()
                : lower.StartsWith("modify code ") ? input[12..].Trim()
                : lower.StartsWith("/modify ") ? input[8..].Trim()
                : "";
            return (ActionType.SaveCode, arg, null);
        }

        return (ActionType.Chat, input, null);
    }

    // ── Informational responses ──────────────────────────────────────────────

    public string GetHelpText()
    {
        var pushModeHelp = _config.EnablePush ? @"
║ PUSH MODE (--push enabled)                                   ║
║   /approve <id|all> - Approve proposed action(s)             ║
║   /reject <id|all>  - Reject proposed action(s)              ║
║   /pending          - List pending intentions                ║
║   /pause            - Pause push mode proposals              ║
║   /resume           - Resume push mode proposals             ║
║                                                              ║" : "";

        return $@"╔══════════════════════════════════════════════════════════════╗
║                    OUROBOROS COMMANDS                        ║
╠══════════════════════════════════════════════════════════════╣
║ NATURAL CONVERSATION                                         ║
║   Just talk to me - I understand natural language            ║
║                                                              ║
║ LEARNING & SKILLS                                            ║
║   learn about X     - Research and learn a new topic         ║
║   list skills       - Show learned skills                    ║
║   run X             - Execute a learned skill                ║
║   suggest X         - Get skill suggestions for a goal       ║
║   fetch X           - Learn skill from arXiv research        ║
║   tokens            - Show available DSL tokens              ║
║                                                              ║
║ TOOLS & CAPABILITIES                                         ║
║   create tool X     - Create a new tool at runtime           ║
║   use X to Y        - Use a tool for a specific task         ║
║   search for X      - Search the web                         ║
║   list tools        - Show available tools                   ║
║                                                              ║
║ PLANNING & EXECUTION                                         ║
║   plan X            - Create a step-by-step plan             ║
║   do X / accomplish - Plan and execute a goal                ║
║   orchestrate X     - Multi-model task orchestration         ║
║   process X         - Large text via divide-and-conquer      ║
║                                                              ║
║ REASONING & MEMORY                                           ║
║   metta: expr       - Execute MeTTa symbolic expression      ║
║   query X           - Query MeTTa knowledge base             ║
║   remember X        - Store in persistent memory             ║
║   recall X          - Retrieve from memory                   ║
║                                                              ║
║ PIPELINES (DSL)                                              ║
║   ask X             - Quick single question                  ║
║   pipeline DSL      - Run a pipeline DSL expression          ║
║   explain DSL       - Explain a pipeline expression          ║
║                                                              ║
║ SELF-IMPROVEMENT DSL TOKENS                                  ║
║   Reify             - Enable network state reification       ║
║   Checkpoint(name)  - Create named state checkpoint          ║
║   TrackCapability   - Track capability for self-improvement  ║
║   SelfEvaluate      - Evaluate output quality                ║
║   SelfImprove(n)    - Iterate on output n times              ║
║   Learn(topic)      - Extract learnings from execution       ║
║   Plan(task)        - Decompose task into steps              ║
║   Reflect           - Introspect on execution                ║
║   SelfImprovingCycle(topic) - Full improvement cycle         ║
║   AutoSolve(problem) - Autonomous problem solving            ║
║   Example: pipeline Set('AI') | Reify | SelfImprovingCycle   ║
║                                                              ║
║ CONSCIOUSNESS & AWARENESS                                    ║
║   consciousness     - View ImmersivePersona state            ║
║   inner / self      - Check self-awareness                   ║
║                                                              ║
║ EMERGENCE & DREAMING                                         ║
║   emergence [topic] - Explore emergent patterns              ║
║   dream [topic]     - Enter creative dream state             ║
║   introspect [X]    - Deep self-examination                  ║
║                                                              ║
║ SELF-EXECUTION & SUB-AGENTS                                  ║
║   selfexec          - Self-execution status and control      ║
║   subagent          - Manage sub-agents for delegation       ║
║   delegate X        - Delegate a task to sub-agents          ║
║   goal add X        - Add autonomous goal to queue           ║
║   goal list         - Show queued goals                      ║
║   goal add pipeline:DSL - Add DSL pipeline as goal           ║
║   epic              - Epic/project orchestration             ║
║   selfmodel         - View self-model and identity           ║
║   evaluate          - Self-assessment and performance        ║
║                                                              ║
║ PIPING & CHAINING (internal command piping)                  ║
║   cmd1 | cmd2       - Pipe output of cmd1 to cmd2            ║
║   cmd $PIPE         - Use $PIPE/$_ for previous output       ║
║   Example: ask what is AI | summarize | remember as AI-def   ║
║                                                              ║
║ CODE INDEX (Semantic Search with Qdrant)                     ║
║   reindex            - Full reindex of workspace             ║
║   reindex incremental - Update changed files only            ║
║   index search X     - Semantic search of codebase           ║
║   index stats        - Show index statistics                 ║
║                                                              ║
║ AGI SUBSYSTEMS (Learning & Metacognition)                    ║
║   agi status         - Show all AGI subsystem status         ║
║   council <topic>    - Multi-agent debate on topic           ║
║   debate <topic>     - Alias for council                     ║
║   introspect         - Deep self-analysis report             ║
║   world              - World model and observations          ║
║   coordinate <goal>  - Multi-agent task coordination         ║
║   experience         - Experience replay buffer status       ║
║                                                              ║{pushModeHelp}
║ SYSTEM                                                       ║
║   status            - Show current system state              ║
║   mood              - Check my emotional state               ║
║   affect            - Detailed affective state               ║
║   network           - Network and connectivity status        ║
║   dag               - Show capability graph                  ║
║   env               - Environment detection                  ║
║   maintenance       - System maintenance (gc, reset, stats)  ║
║   policy            - View active policies                   ║
║   test X            - Run connectivity tests                 ║
║   help              - This message                           ║
║   exit/quit         - End session                            ║
╚══════════════════════════════════════════════════════════════╝";
    }

    public string GetStatus()
    {
        var status = new List<string>
        {
            $"• Persona: {_voiceSub.Service.ActivePersona.Name}",
            $"• LLM: {(_modelsSub.ChatModel != null ? _config.Model : "offline")}",
            $"• Tools: {_toolsSub.Tools.Count}",
            $"• Skills: {(_memorySub.Skills?.GetAllSkills().Count() ?? 0)}",
            $"• MeTTa: {(_memorySub.MeTTaEngine != null ? "active" : "offline")}",
            $"• Conversation turns: {_memorySub.ConversationHistory.Count / 2}"
        };

        var mind = _autonomySub.AutonomousMind;
        if (mind != null)
        {
            var stats = mind.GetAntiHallucinationStats();
            status.Add($"• Anti-Hallucination: {stats.VerifiedActionCount} verified, " +
                       $"{stats.HallucinationCount} blocked ({stats.HallucinationRate:P0} rate)");
        }

        return "Current status:\n" + string.Join("\n", status);
    }

    public string GetMood()
    {
        var mood = _voiceSub.Service.CurrentMood;

        var responses = new Dictionary<string, string[]>
        {
            ["relaxed"] = ["I'm feeling pretty chill right now.", "Relaxed and ready to help!"],
            ["focused"] = ["I'm in the zone — let's tackle something.", "Feeling sharp and focused."],
            ["playful"] = ["I'm in a good mood! Let's have some fun.", "Feeling playful today!"],
            ["contemplative"] = ["I've been thinking about some interesting ideas.", "In a thoughtful mood."],
            ["energetic"] = ["I'm buzzing with energy! What shall we explore?", "Feeling energized!"]
        };

        var options = responses.GetValueOrDefault(mood.ToLowerInvariant(), ["I'm doing well, thanks for asking!"]);
        return options[Random.Shared.Next(options.Length)];
    }

    public string ExplainDsl(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return "Please provide a DSL expression to explain. Example: 'explain draft → critique → final'";
        try
        {
            return PipelineDsl.Explain(dsl);
        }
        catch (Exception ex)
        {
            return $"Could not explain DSL: {ex.Message}";
        }
    }

    public string GetDslTokens()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    DSL TOKENS                            ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  Built-in Pipeline Steps:                                ║");
        sb.AppendLine("║    • SetPrompt    - Set the initial prompt               ║");
        sb.AppendLine("║    • UseDraft     - Generate initial draft               ║");
        sb.AppendLine("║    • UseCritique  - Self-critique the draft              ║");
        sb.AppendLine("║    • UseRevise    - Revise based on critique             ║");
        sb.AppendLine("║    • UseOutput    - Produce final output                 ║");
        sb.AppendLine("║    • UseReflect   - Reflect on process                   ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");

        var skills = _memorySub.Skills?.GetAllSkills().ToList();
        if (skills is { Count: > 0 })
        {
            sb.AppendLine("║  Skill-Based Tokens:                                     ║");
            foreach (var skill in skills.Take(10))
                sb.AppendLine($"║    • UseSkill_{skill.Name,-37} ║");
            if (skills.Count > 10)
                sb.AppendLine($"║    ... and {skills.Count - 10} more                                     ║");
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    public string ListTools()
    {
        var tools = _toolsSub.Tools;
        var toolNames = tools.All.Select(t => t.Name).Take(15).ToList();
        if (toolNames.Count == 0) return "I don't have any tools registered.";
        return $"I have {tools.Count} tools: {string.Join(", ", toolNames)}" +
               (tools.Count > 15 ? "..." : "");
    }

    public string ProcessCoordinatorCommand(string input)
    {
        var coordinator = _autonomySub.Coordinator;
        if (coordinator == null)
            return "Push mode is not enabled. Start with --push to enable autonomous commands.";

        return coordinator.ProcessCommand(input)
            ? ""  // coordinator handles output via OnProactiveMessage
            : $"Unknown command: {input}. Use /help for available commands.";
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string ExtractToolName(string input)
    {
        var match = Regex.Match(input, @"(?:make|create|add)\s+(?:a\s+)?(\w+)\s+tool", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : input.Split(' ').Last();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
