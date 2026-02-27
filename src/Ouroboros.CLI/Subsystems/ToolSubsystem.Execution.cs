// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text.RegularExpressions;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;

public sealed partial class ToolSubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // TOOL EXECUTION — auto-tool, thought-driven, and UI-gated invocation
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

                    var ptzInvoke = await ExecuteWithUiAsync(ptzTool, ptzCommand, ptzCommand);
                    if (ptzInvoke is null)
                        return "PTZ MOVEMENT DENIED: User rejected the camera movement request.";
                    var ptzOutput = ptzInvoke.Value.Match(ok => ok, err => $"[PTZ error: {err}]");
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
                    var captureInvoke = await ExecuteWithUiAsync(cameraTool, "", "capture frame");
                    if (captureInvoke is null)
                        return "CAMERA CAPTURE DENIED: User rejected camera access.";
                    var captureOutput = captureInvoke.Value.Match(ok => ok, err => $"[Camera error: {err}]");
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

                    var smartInvoke = await ExecuteWithUiAsync(smartHomeTool, smartCommand, smartCommand);
                    if (smartInvoke is null)
                        return "SMART HOME DENIED: User rejected the smart home command.";
                    var smartOutput = smartInvoke.Value.Match(ok => ok, err => $"[Smart home error: {err}]");
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
                var searchResultResult = await ExecuteWithUiAsync(searchTool, searchTerm, searchTerm);
                var searchResult = searchResultResult?.Match(ok => ok, err => null);
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
                                var fileInvoke = await ExecuteWithUiAsync(readTool, fileMatch.Value, fileMatch.Value);
                                var fileContent = fileInvoke?.Match(ok => ok, err => null);
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
                        var invoke = await ExecuteWithUiAsync(searchTool, searchTarget, searchTarget);
                        var content = invoke?.Match(ok => ok, err => null);
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
                        var invoke = await ExecuteWithUiAsync(readTool, fileMatch.Value, fileMatch.Value);
                        var content = invoke?.Match(ok => ok, err => null);
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
                        var invoke = await ExecuteWithUiAsync(calcTool, mathMatch.Value.Trim(), mathMatch.Value.Trim());
                        var content = invoke?.Match(ok => ok, err => null);
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
                    var invoke = await ExecuteWithUiAsync(fetchTool, urlMatch.Value, urlMatch.Value);
                    var content = invoke?.Match(ok => ok, err => null);
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
                        var invoke = await ExecuteWithUiAsync(webTool, searchTarget, searchTarget);
                        var content = invoke?.Match(ok => ok, err => null);
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
                    var invoke = await ExecuteWithUiAsync(qdrantTool, "{\"command\":\"status\"}", "status");
                    var content = invoke?.Match(ok => ok, err => null);
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
    /// Executes a tool with full Crush-style UI pipeline:
    ///   1. <c>●</c> pending header via <see cref="IConsoleOutput.WriteToolCall"/>
    ///   2. Permission check for sensitive tools via <see cref="SubsystemInitContext.PermissionBroker"/>
    ///   3. Tool invocation
    ///   4. <c>✓/✗</c> result line via <see cref="IConsoleOutput.WriteToolResult"/>
    ///   5. <see cref="Infrastructure.ToolCompletedEvent"/> published on the agent event bus
    /// Returns <c>null</c> when the user denied the call.
    /// </summary>
    internal async Task<Ouroboros.Abstractions.Monads.Result<string, string>?> ExecuteWithUiAsync(
        ITool tool,
        string args,
        string? displayParam = null,
        CancellationToken ct = default)
    {
        var label = displayParam ?? (args.Length > 60 ? args[..59] + "…" : args);
        Output.WriteToolCall(tool.Name, label);

        // Permission gate: default-on for all tools except known safe/read-only ones
        if (!ExemptTools.Contains(tool.Name) && Ctx.PermissionBroker != null)
        {
            var action = await Ctx.PermissionBroker.RequestAsync(tool.Name, "invoke", label, ct);
            if (action == Infrastructure.PermissionAction.Deny)
            {
                Output.WriteToolResult(tool.Name, false, "Denied by user");
                Ctx.AgentEventBus?.Publish(new Infrastructure.ToolCompletedEvent(tool.Name, false, "denied", TimeSpan.Zero));
                return null;
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await tool.InvokeAsync(args, ct);
        sw.Stop();

        var outputText = result.Match(ok => ok, err => $"error: {err}");
        Output.WriteToolResult(tool.Name, result.IsSuccess, outputText);
        Ctx.AgentEventBus?.Publish(new Infrastructure.ToolCompletedEvent(tool.Name, result.IsSuccess, outputText, sw.Elapsed));

        return result;
    }

    /// <summary>
    /// Wires the Crush-inspired <see cref="ToolAwareChatModel.BeforeInvoke"/> and
    /// <see cref="ToolAwareChatModel.AfterInvoke"/> hooks onto <paramref name="llm"/>
    /// so LLM-driven tool calls go through the same UI and event pipeline.
    /// </summary>
    internal void WireHooks(ToolAwareChatModel llm)
    {
        var broker = Ctx.PermissionBroker;
        var bus    = Ctx.AgentEventBus;

        if (broker != null)
        {
            llm.BeforeInvoke = async (toolName, args, ct) =>
            {
                if (ExemptTools.Contains(toolName)) return true;
                Output.WriteToolCall(toolName, args.Length > 60 ? args[..59] + "…" : args);
                var action = await broker.RequestAsync(toolName, "invoke", args, ct);
                return action == Infrastructure.PermissionAction.Allow;
            };
        }

        if (bus != null || broker != null)
        {
            llm.AfterInvoke = (toolName, _, output, elapsed, success) =>
            {
                // Only show result line for non-exempt tools (already shown by BeforeInvoke path)
                if (!ExemptTools.Contains(toolName))
                    Output.WriteToolResult(toolName, success, output);

                bus?.Publish(new Infrastructure.ToolCompletedEvent(toolName, success, output, elapsed));
            };
        }
    }
}
