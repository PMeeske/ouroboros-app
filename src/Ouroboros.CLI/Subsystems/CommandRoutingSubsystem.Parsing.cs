// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.CLI.Commands;

/// <summary>
/// Partial class for command routing: input parsing and action classification.
/// </summary>
public sealed partial class CommandRoutingSubsystem
{
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

        // Swarm orchestration
        if (lower.StartsWith("swarm ") || lower == "swarm")
            return (ActionType.Swarm, lower.StartsWith("swarm ") ? input[6..].Trim() : "", null);

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
}
