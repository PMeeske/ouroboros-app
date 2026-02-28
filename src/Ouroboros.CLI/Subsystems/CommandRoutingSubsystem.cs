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
public sealed partial class CommandRoutingSubsystem : ICommandRoutingSubsystem
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
        catch (OperationCanceledException) { throw; }
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
        var match = CreateToolNameRegex().Match(input);
        return match.Success ? match.Groups[1].Value : input.Split(' ').Last();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [GeneratedRegex(@"(?:make|create|add)\s+(?:a\s+)?(\w+)\s+tool", RegexOptions.IgnoreCase)]
    private static partial Regex CreateToolNameRegex();
}
