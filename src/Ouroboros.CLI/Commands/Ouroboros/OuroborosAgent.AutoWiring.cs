// <copyright file="OuroborosAgent.AutoWiring.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Autonomy-related cross-subsystem wiring: mind delegates, coordinator, self-execution,
/// push mode, and the autonomous action engine.
/// </summary>
public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Wires agent-level callbacks into the autonomy subsystem so it can access
    /// cross-cutting concerns (voice, chat, conversation state).
    /// </summary>
    private void WireAutonomyCallbacks()
    {
        _autonomySub.IsInConversationLoop = () => _isInConversationLoop;
        _autonomySub.SayAndWaitAsyncFunc = (text, persona) => SayAndWaitAsync(text, persona);
        _autonomySub.AnnounceAction = Announce;
        _autonomySub.ChatAsyncFunc = ChatAsync;
        _autonomySub.GetLanguageNameFunc = GetLanguageName;
        _autonomySub.StartListeningAsyncFunc = () => StartListeningAsync();
        _autonomySub.StopListeningAsyncAction = StopListeningAsync;
    }

    /// <summary>
    /// Wires AutonomousMind's ThinkFunction, SearchFunction, ExecuteToolFunction,
    /// and proactive thought/message event handlers.
    /// </summary>
    private async Task WireAutonomousMindDelegatesAsync()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.ThinkFunction = async (prompt, token) =>
        {
            var actualPrompt = prompt;
            if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
            {
                var languageName = GetLanguageName(_config.Culture);
                actualPrompt = $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n{prompt}";
            }
            return await GenerateWithOrchestrationAsync(actualPrompt, token);
        };

        _autonomousMind.SearchFunction = async (query, token) =>
        {
            var searchTool = _toolFactory?.CreateWebSearchTool("duckduckgo");
            if (searchTool != null)
            {
                var result = await searchTool.InvokeAsync(query, token);
                return result.Match(s => s, _ => "");
            }
            return "";
        };

        _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
        {
            var tool = _tools.Get(toolName);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(input, token);
                return result.Match(s => s, e => $"Error: {e}");
            }
            return "Tool not found";
        };

        // Register subsystem instances in the Scrutor-backed IServiceProvider
        // so ServiceDiscoveryTool can list and invoke them at runtime.
        Ouroboros.Application.Tools.ServiceContainerFactory.RegisterSingleton(_autonomousMind);

        // Seed baseline interests so autonomous learning keeps research/philosophy active.
        _autonomousMind.AddInterest("research");
        _autonomousMind.AddInterest("philosophy");
        _autonomousMind.AddInterest("epistemology");
        _autonomousMind.AddInterest("self-improvement");

        _autonomousMind.VerifyFileExistsFunction = (path) =>
        {
            var absolutePath = Path.IsPathRooted(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
            return File.Exists(absolutePath);
        };

        _autonomousMind.ComputeFileHashFunction = (path) =>
        {
            try
            {
                var absolutePath = Path.IsPathRooted(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
                if (!File.Exists(absolutePath)) return null;
                using var stream = File.OpenRead(absolutePath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hash = sha256.ComputeHash(stream);
                return Convert.ToBase64String(hash);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        };

        _autonomousMind.ExecutePipeCommandFunction = async (pipeCommand, token) =>
        {
            try { return await ProcessInputWithPipingAsync(pipeCommand); }
            catch (Exception ex) { return $"Pipe execution failed: {ex.Message}"; }
        };

        _autonomousMind.SanitizeOutputFunction = async (rawOutput, token) =>
        {
            if (_chatModel == null || string.IsNullOrWhiteSpace(rawOutput))
                return rawOutput;
            try
            {
                string prompt = PromptResources.SummarizeToolOutput(rawOutput);
                string sanitized = await _chatModel.GenerateTextAsync(prompt, token);
                return string.IsNullOrWhiteSpace(sanitized) ? rawOutput : sanitized.Trim();
            }
            catch (Exception) { return rawOutput; }
        };

        // Wire limitation-busting tools via shared context
        var mindCtx = AutonomousTools.DefaultContext;
        mindCtx.SearchFunction = _autonomousMind.SearchFunction;
        mindCtx.EvaluateFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        mindCtx.ReasonFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        mindCtx.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
        mindCtx.SummarizeFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        mindCtx.CritiqueFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        mindCtx.OllamaFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";

        // Proactive message events -- track, whisper, and flow into conversation memory
        _autonomousMind.OnProactiveMessage += async (msg) =>
        {
            var thoughtContent = msg.TrimStart();
            if (thoughtContent.StartsWith("💡") || thoughtContent.StartsWith("💬") ||
                thoughtContent.StartsWith("🤔") || thoughtContent.StartsWith("💭"))
                thoughtContent = thoughtContent[2..].Trim();
            TrackLastThought(thoughtContent);

            // Flow into conversation memory so the LLM sees its own background thoughts
            _conversationHistory.Add($"[Inner thought] {thoughtContent}");

            // Push the proactive thought as Iaret's StatusText -- these are the most
            // conversation-contextual LLM-generated thoughts (research findings, feelings).
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: thoughtContent);

            try { await _voice.WhisperAsync(msg); } catch (Exception) { }
        };

        // Display only research (Curiosity/Observation) and inner feelings (Reflection) thoughts.
        // Algorithmic action/strategic/sharing thoughts are suppressed.
        _autonomousMind.OnThought += async (thought) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
            var thoughtType = thought.Type switch
            {
                Ouroboros.Application.Services.ThoughtType.Reflection => InnerThoughtType.SelfReflection,
                Ouroboros.Application.Services.ThoughtType.Curiosity => InnerThoughtType.Curiosity,
                Ouroboros.Application.Services.ThoughtType.Observation => InnerThoughtType.Observation,
                Ouroboros.Application.Services.ThoughtType.Creative => InnerThoughtType.Creative,
                Ouroboros.Application.Services.ThoughtType.Sharing => InnerThoughtType.Synthesis,
                Ouroboros.Application.Services.ThoughtType.Action => InnerThoughtType.Strategic,
                _ => InnerThoughtType.Wandering
            };
            var innerThought = InnerThought.CreateAutonomous(thoughtType, thought.Content, confidence: 0.7);

            // Only surface research and emotional thoughts; suppress strategic/action/synthesis noise
            bool isResearch = thought.Type is
                Ouroboros.Application.Services.ThoughtType.Curiosity or
                Ouroboros.Application.Services.ThoughtType.Observation;
            bool isFeeling = thought.Type is Ouroboros.Application.Services.ThoughtType.Reflection;

            // Flow research, feelings, and creative thoughts into conversation memory
            if (isResearch || isFeeling || thought.Type is Ouroboros.Application.Services.ThoughtType.Creative)
            {
                _conversationHistory.Add($"[{thought.Type}] {thought.Content}");
            }

            if (isResearch || isFeeling)
            {
                if (_config.Verbosity != OutputVerbosity.Quiet)
                {
                    AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]{Markup.Escape($"💭 {thought.Content}")}[/]");
                }

                // Push the real thought as Iaret's StatusText
                if (_avatarService is { } svc)
                    svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: thought.Content);
            }

            await PersistThoughtAsync(innerThought, "autonomous_thinking");
        };

        // InnerDialogEngine (algorithmic thought generation) intentionally not connected.
    }

    /// <summary>
    /// Wires AutonomousCoordinator with tool execution, thinking, embedding,
    /// Qdrant storage, MeTTa reasoning, chat processing, and voice control.
    /// </summary>
    private async Task WireAutonomousCoordinatorAsync()
    {
        if (_autonomousCoordinator == null)
        {
            try
            {
                await InitializeAutonomousCoordinatorAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  ⚠ Autonomous Coordinator wiring failed: {ex.Message}"));
            }
            return;
        }
        // Coordinator was created by AutonomySubsystem; just wire delegates here
    }

    /// <summary>
    /// Wires self-execution background loop.
    /// </summary>
    private void WireSelfExecution()
    {
        _selfExecutionCts?.Dispose();
        _selfExecutionCts = new CancellationTokenSource();
        _selfExecutionEnabled = true;
        _selfExecutionTask = Task.Run(_autonomySub.SelfExecutionLoopAsync, _selfExecutionCts.Token);
        _output.RecordInit("Self-Execution", true, "autonomous goal pursuit");
    }

    /// <summary>
    /// Starts push mode by activating the coordinator tick loop.
    /// </summary>
    private void WirePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            _output.WriteWarning("Cannot start Push Mode: Coordinator not initialized");
            return;
        }
        try
        {
            _autonomousCoordinator.Start();
            _pushModeCts?.Dispose();
            _pushModeCts = new CancellationTokenSource();
            _pushModeTask = Task.Run(() => _autonomySub.PushModeLoopAsync(_pushModeCts.Token), _pushModeCts.Token);
            var yoloPart = _config.YoloMode ? ", YOLO" : "";
            _output.RecordInit("Push Mode", true, $"interval: {_config.IntentionIntervalSeconds}s{yoloPart}");
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"Push Mode start failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Wires the <see cref="AutonomousActionEngine"/> with the LLM and full self-awareness context,
    /// then starts its 3-minute loop. On by default -- no config flag required.
    ///
    /// Self-awareness context passed on each cycle:
    ///   - Persona name + current mood/energy
    ///   - Active personality traits
    ///   - Recent conversation history (last 6 lines)
    ///   - Last autonomous thought
    /// </summary>
    private void WireAutonomousActionEngine()
    {
        if (_actionEngine is null) return;

        // ThinkFunction: use the same orchestrated model as autonomous mind
        _actionEngine.ThinkFunction = async (prompt, ct) =>
        {
            var actualPrompt = prompt;
            if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
            {
                var lang = GetLanguageName(_config.Culture);
                actualPrompt = $"LANGUAGE: Respond ONLY in {lang}. No English.\n\n{prompt}";
            }
            return await GenerateWithOrchestrationAsync(actualPrompt, ct);
        };

        // ExecuteFunc: full agent pipeline -- pipe DSL, tool calls, everything
        _actionEngine.ExecuteFunc = async (command, ct) =>
        {
            try { return await ProcessInputWithPipingAsync(command); }
            catch (Exception ex) { return $"[Pipeline error: {ex.Message}]"; }
        };

        // GetAvailableToolsFunc: expose registered tool names
        _actionEngine.GetAvailableToolsFunc = () =>
            _tools.All.Select(t => t.Name).ToList();

        // GetContextFunc: rich self-awareness snapshot
        _actionEngine.GetContextFunc = () =>
        {
            var lines = new List<string>();

            var persona = _voice.ActivePersona;
            if (persona is not null)
            {
                lines.Add($"[Persona] Name: {persona.Name}");
                if (persona.Moods?.FirstOrDefault() is { } mood)
                    lines.Add($"[Persona] Current mood: {mood}");
            }

            if (_personality?.Traits is { Count: > 0 })
            {
                var top = _personality.GetActiveTraits(3).Select(t => t.Name);
                lines.Add($"[Personality] Active traits: {string.Join(", ", top)}");
            }

            if (_valenceMonitor is not null)
                lines.Add($"[Affect] Valence: {_valenceMonitor.GetCurrentState().Valence:F2}");

            if (!string.IsNullOrWhiteSpace(_lastThoughtContent))
                lines.Add($"[Last thought] {_lastThoughtContent}");

            foreach (var line in _conversationHistory.TakeLast(6))
                lines.Add(line);

            return lines;
        };

        // OnAction: display as [Autonomous] with action + result, flow into memory, persist
        _actionEngine.OnAction += async (reason, result) =>
        {
            if (string.IsNullOrWhiteSpace(reason)) return;

            // Flow autonomous actions into conversation memory so the LLM is aware of what it did
            var actionSummary = string.IsNullOrWhiteSpace(result)
                ? $"[Autonomous action] {reason}"
                : $"[Autonomous action] {reason} → {(result.Length > 200 ? result[..200] + "…" : result)}";
            _conversationHistory.Add(actionSummary);

            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                AnsiConsole.MarkupLine(
                    $"\n  [bold rgb(0,200,160)][[Autonomous]][/] 💬 {Markup.Escape(reason)}");

                if (!string.IsNullOrWhiteSpace(result))
                {
                    // Truncate long results for display
                    var display = result.Length > 400 ? result[..400] + "…" : result;
                    AnsiConsole.MarkupLine(
                        $"  [dim][rgb(0,200,160)]↳ {Markup.Escape(display)}[/][/]");
                }
            }

            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy,
                    svc.CurrentState.Positivity, statusText: reason);

            var content = string.IsNullOrWhiteSpace(result) ? reason : $"{reason}\n\nResult: {result}";
            var thought = InnerThought.CreateAutonomous(InnerThoughtType.Intention, content, confidence: 0.8);
            await PersistThoughtAsync(thought, "autonomous_action");
        };

        // Start the loop
        _actionEngine.Start();
        _output.RecordInit("Autonomous Action Engine", true,
            $"started — interval: {_actionEngine.Interval.TotalMinutes:F0} min, full DSL access");
    }
}
