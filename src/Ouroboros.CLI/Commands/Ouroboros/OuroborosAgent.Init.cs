// <copyright file="OuroborosAgent.Init.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Initializes all agent subsystems via mediator delegation.
    /// Each subsystem self-initializes; agent wires cross-subsystem dependencies.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Set static culture for TTS in static methods
        SetStaticCulture(_config.Culture);

        _output.WriteWelcome(_config.Persona, _config.Model,
            _voice.ActivePersona?.Moods?.FirstOrDefault());

        // Print feature configuration (verbose only)
        if (_config.Verbosity == OutputVerbosity.Verbose)
            PrintFeatureStatus();

        // Crush-inspired: shared permission broker and agent event bus.
        // SkipAll mirrors the existing YoloMode flag (--yolo = bypass all approvals).
        var permissionBroker = new CLI.Infrastructure.ToolPermissionBroker
        {
            SkipAll = _config.YoloMode,
        };
        _permissionBroker = permissionBroker;
        var agentEventBus = new CLI.Infrastructure.EventBroker<CLI.Infrastructure.AgentEvent>();

        // Create shared initialization context (mediator pattern)
        var ctx = new Subsystems.SubsystemInitContext
        {
            Config = _config,
            Output = _output,
            VoiceService = _voice,
            StaticConfiguration = _staticConfiguration,
            Services = _serviceProvider,
            Voice = _voiceSub,
            Models = _modelsSub,
            Tools = _toolsSub,
            Memory = _memorySub,
            Cognitive = _cognitiveSub,
            Autonomy = _autonomySub,
            Embodiment = _embodimentSub,
            RegisterCameraCaptureAction = () => RegisterCameraCaptureTool(),
            PermissionBroker = permissionBroker,
            AgentEventBus    = agentEventBus,
        };

        // ── Phase 1: Infrastructure (standalone) ──
        if (_config.Voice)
            await _voice.InitializeAsync();

        _voiceSub.SpeakWithSapiFunc = SpeakWithSapiAsync;
        await _voiceSub.InitializeAsync(ctx);

        // ── Phase 2: Models (standalone) ──
        await _modelsSub.InitializeAsync(ctx);

        // ── Phase 2.5: Localization + Language detection (need Config; Language Llm is remote) ──
        await _localizationSub.InitializeAsync(ctx);
        await _languageSub.InitializeAsync(ctx);

        // Wire LLM sanitizer on voice side channel
        if (_config.VoiceChannel && _voiceSideChannel != null && _chatModel != null)
        {
            _voiceSideChannel.SetLlmSanitizer(async (prompt, ct) =>
                await _chatModel.GenerateTextAsync(prompt, ct));
            _output.RecordInit("Voice LLM Sanitizer", true, "natural speech condensation");
        }

        // ── Phase 3: Tools (needs Models) ──
        await _toolsSub.InitializeAsync(ctx);

        // ── Phase 3.1: Claude-style meta-tools (plan / ask / bypass) ──
        RegisterClaudeStyleTools();

        // ── Phase 4: Memory (needs Models, uses MeTTa from Tools) ──
        await _memorySub.InitializeAsync(ctx);

        // ── Phase 5: Cognitive (needs Models + Memory + Tools) ──
        await _cognitiveSub.InitializeAsync(ctx);

        // ── Phase 6: Autonomy (needs all above) ──
        await _autonomySub.InitializeAsync(ctx);

        // ── Phase 7: Embodiment (needs Memory + Autonomy) ──
        await _embodimentSub.InitializeAsync(ctx);

        // ── Phase 7.1: SelfAssembly (needs Autonomy; Llm + History wired after Phase 8) ──
        await _selfAssemblySub.InitializeAsync(ctx);

        // ── Phase 7.2: PipeProcessing (needs Config; ProcessInputFunc wired after Phase 8) ──
        await _pipeSub.InitializeAsync(ctx);

        // ── Phase 7.3: Chat (needs all original subsystems) ──
        await _chatSub.InitializeAsync(ctx);

        // ── Phase 7.4: CommandRouting (needs Tools, Memory, Autonomy) ──
        await _commandRoutingSub.InitializeAsync(ctx);

        // ── Phase 8: Cross-subsystem wiring (mediator orchestration) ──
        WireCrossSubsystemDependencies();

        // ── Phase 8.5: Agent event bridge (MediatR notification pipeline) ──
        WireAgentEventBridge(agentEventBus);

        // ── Phase 9: Post-init actions ──
        _isInitialized = true;
        _output.FlushInitSummary();
        if (_config.Verbosity != OutputVerbosity.Quiet)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n  ✓ Ouroboros fully initialized\n"));
            PrintQuickHelp();
        }

        // AGI warmup - prime the model with examples for autonomous operation
        await PerformAgiWarmupAsync();

        // Enforce policies if self-modification is enabled
        if (_config.EnableSelfModification)
            await EnforceGovernancePoliciesAsync();

        // Start listening for voice input if enabled via CLI
        if (_config.Listen)
        {
            _output.WriteSystem("🎤 Voice listening enabled via --listen flag");
            await StartListeningAsync();
        }
    }

    /// <summary>
    /// Wires cross-subsystem dependencies that require mediator orchestration.
    /// This is the core of the mediator pattern — connecting subsystem components.
    /// </summary>

    private void WireCrossSubsystemDependencies()
    {
        // ── Autonomy subsystem cross-references ──
        WireAutonomyCallbacks();

        // ── Autonomous Mind delegates ──
        WireAutonomousMindDelegatesAsync().GetAwaiter().GetResult();

        // ── Autonomous Action Engine (on by default, 3-min interval) ──
        WireAutonomousActionEngine();

        // ── Autonomous Coordinator ──
        WireAutonomousCoordinatorAsync().GetAwaiter().GetResult();

        // ── Self-Execution ──
        if (_config.EnableMind)
            WireSelfExecution();

        // ── Push Mode ──
        if (_config.EnablePush)
            WirePushMode();

        // ── Presence Detection events ──
        WirePresenceDetection();

        // -- Persona events --
        WirePersonaEvents();

        // -- SystemAccessTools shared state --
        WireSystemAccessTools();

        // -- Network state persistence delegates --
        WireNetworkPersistence();

        // -- Avatar mood transitions --
        WireAvatarMoodTransitions();

        // -- Pipeline think delegate --
        WirePipelineThinkDelegate();

        // -- Memory subsystem thought persistence --
        _memorySub.PersistThoughtFunc = PersistThoughtAsync;

        // ── Localization: wire LLM for thought translation ──
        _localizationSub.Llm = _llm;

        // ── SelfAssembly: wire LLM, conversation history, and event callbacks ──
        _selfAssemblySub.Llm = _llm;
        _selfAssemblySub.ConversationHistory = _memorySub.ConversationHistory;
        _selfAssemblySub.WireCallbacks();

        // ── PipeProcessing: wire the central input dispatch function ──
        _pipeSub.ProcessInputFunc = ProcessInputAsync;

        // ── Chat: wire persistence delegates and language lookup ──
        _chatSub.PersistThoughtFunc = PersistThoughtAsync;
        _chatSub.PersistThoughtResultFunc = (id, type, content, success, confidence) =>
            PersistThoughtResultAsync(id, type, content, success, confidence);
        _chatSub.GetLanguageNameFunc = _localizationSub.GetLanguageName;
    }

    /// <summary>
    /// Wires agent-level callbacks into the autonomy subsystem so it can access
    /// cross-cutting concerns (voice, chat, conversation state).

    private void WireAutonomyCallbacks()
    {
        _autonomySub.IsInConversationLoop = () => _isInConversationLoop;
        _autonomySub.SayAndWaitAsyncFunc = (text, persona) => SayAndWaitAsync(text, persona);
        _autonomySub.AnnounceAction = Announce;
        _autonomySub.ChatAsyncFunc = ChatAsync;
        _autonomySub.GetLanguageNameFunc = GetLanguageName;
        _autonomySub.StartListeningAsyncFunc = () => StartListeningAsync();
        _autonomySub.StopListeningAction = StopListening;
    }

    /// <summary>
    /// Wires AutonomousMind's ThinkFunction, SearchFunction, ExecuteToolFunction,

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
            catch { return null; }
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
            catch { return rawOutput; }
        };

        // Wire limitation-busting tools
        AutonomousTools.VerifyClaimTool.SearchFunction = _autonomousMind.SearchFunction;
        AutonomousTools.VerifyClaimTool.EvaluateFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ReasoningChainTool.ReasonFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ParallelToolsTool.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
        AutonomousTools.CompressContextTool.SummarizeFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.SelfDoubtTool.CritiqueFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.ParallelMeTTaThinkTool.OllamaFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
        AutonomousTools.OuroborosMeTTaTool.OllamaFunction = async (prompt, token) =>
            _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";

        // Proactive message events — track and whisper only; don't display in console
        _autonomousMind.OnProactiveMessage += async (msg) =>
        {
            var thoughtContent = msg.TrimStart();
            if (thoughtContent.StartsWith("💡") || thoughtContent.StartsWith("💬") ||
                thoughtContent.StartsWith("🤔") || thoughtContent.StartsWith("💭"))
                thoughtContent = thoughtContent[2..].Trim();
            TrackLastThought(thoughtContent);

            // Push the proactive thought as Iaret's StatusText — these are the most
            // conversation-contextual LLM-generated thoughts (research findings, feelings).
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: thoughtContent);

            try { await _voice.WhisperAsync(msg); } catch { }
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
    /// Wires presence detector events (HandlePresenceDetectedAsync, absence tracking).
    /// </summary>

    private void WirePresenceDetection()
    {
        if (_presenceDetector == null) return;

        _presenceDetector.OnPresenceDetected += async evt =>
        {
            await HandlePresenceDetectedAsync(evt);
        };

        _presenceDetector.OnAbsenceDetected += evt =>
        {
            _userWasPresent = false;
            System.Diagnostics.Debug.WriteLine($"[Presence] User absence detected via {evt.Source}");
        };
    }

        /// <summary>
    /// Wires ImmersivePersona events (AutonomousThought, ConsciousnessShift).
    /// </summary>

    private void WirePersonaEvents()
    {
        if (_immersivePersona == null) return;

        // ImmersivePersona autonomous thoughts — surface research/feelings, skip pure templates.
        // Metacognitive and Musing types come from InnerDialogEngine string templates
        // ("I notice that I tend to {0}") and are not genuine LLM thoughts — skip those.
        _immersivePersona.AutonomousThought += (_, e) =>
        {
            if (e.Thought.Type is not (InnerThoughtType.Curiosity or InnerThoughtType.Observation or InnerThoughtType.SelfReflection))
                return;

            var content = e.Thought.Content;

            // Filter: skip empty, very short, or unresolved template placeholders.
            // Template artifacts contain bracket-enclosed tags like "[Symbolic context: ...]"
            // that were never substituted with real content.
            if (string.IsNullOrWhiteSpace(content) || content.Length < 12)
                return;
            var bracketIdx = content.IndexOf('[');
            if (bracketIdx >= 0 && content.IndexOf(':', bracketIdx) > bracketIdx)
                return;

            // NOTE: when ImmersiveMode is running, ImmersiveSubsystem.WirePersonaEvents also
            // subscribes to this persona and will print the same thought within milliseconds.
            // ImmersiveSubsystem's dedup guard (8 s window) suppresses the duplicate print.
            AnsiConsole.MarkupLine($"\n  [rgb(128,0,180)]{Markup.Escape($"💭 {content}")}[/]");

            // Push genuine persona thoughts to avatar — excludes Metacognitive/Musing
            // templates which are filled from topic keywords, not LLM generation.
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: content);
        };
    }

    /// <summary>
    /// Wires SystemAccessTools shared static state (SharedPersistence, SharedMind, SharedIndexer).

    private void WireSystemAccessTools()
    {
        if (_selfPersistence != null && _autonomousMind != null)
        {
            SystemAccessTools.SharedPersistence = _selfPersistence;
            SystemAccessTools.SharedMind = _autonomousMind;
        }

        if (_selfIndexer != null)
        {
            SystemAccessTools.SharedIndexer = _selfIndexer;
            _selfIndexer.OnFileIndexed += (file, chunks) =>
                _output.WriteDebug($"[Index] {System.IO.Path.GetFileName(file)} ({chunks} chunks)");
        }
    }

    /// <summary>
    /// Wires AutonomousMind PersistLearningFunction and PersistEmotionFunction
    /// to the PersistentNetworkStateProjector.

    private void WireNetworkPersistence()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.PersistLearningFunction = async (category, content, confidence, token) =>
        {
            if (_networkProjector != null)
            {
                await _networkProjector.RecordLearningAsync(
                    category,
                    content,
                    "autonomous_mind",
                    confidence,
                    token);
            }
        };

        _autonomousMind.PersistEmotionFunction = async (emotion, token) =>
        {
            if (_networkProjector != null)
            {
                await _networkProjector.RecordLearningAsync(
                    "emotional_state",
                    $"Emotion: {emotion.DominantEmotion} (arousal={emotion.Arousal:F2}, valence={emotion.Valence:F2}) - {emotion.Description}",
                    "autonomous_mind",
                    0.6,
                    token);
            }
        };

        // Additional events for debugging/diagnostics
        _autonomousMind.OnDiscovery += async (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                AnsiConsole.MarkupLine($"  [rgb(128,0,180)]{Markup.Escape($"💭 [inner thought] I just learned from '{query}': {fact}")}[/]");
            }

            var discoveryThought = InnerThought.CreateAutonomous(
                InnerThoughtType.Consolidation,
                $"Discovered: {fact} (from query: {query})",
                confidence: 0.8);
            await PersistThoughtAsync(discoveryThought, "discovery");
        };

        _autonomousMind.OnEmotionalChange += (emotion) =>
        {
            _output.WriteDebug($"[mind] Emotional shift: {emotion.DominantEmotion} ({emotion.Description})");
        };

        _autonomousMind.OnStatePersisted += (msg) =>
        {
            System.Diagnostics.Debug.WriteLine($"[State] {msg}");
        };
    }

    /// <summary>
    /// Wires consciousness shift events to the avatar service for mood transitions.
    /// </summary>

    private void WireAvatarMoodTransitions()
    {
        if (_immersivePersona == null || _avatarService == null) return;

        _immersivePersona.ConsciousnessShift += (_, e) =>
        {
            _avatarService?.NotifyMoodChange(
                e.NewEmotion ?? "neutral",
                0.5 + (e.ArousalChange * 0.5),
                e.NewEmotion?.Contains("warm") == true || e.NewEmotion?.Contains("gentle") == true ? 0.8 : 0.5);
        };
    }

    /// <summary>
    /// Creates and wires the <see cref="AgentEventBridge"/>, connecting every existing
    /// event source (PresenceDetector, RoomIntentBus, ImmersivePersona, application
    /// EventBus, CLI EventBroker) into the MediatR notification pipeline so Iaret and
    /// any other subsystem can react via <c>INotificationHandler&lt;T&gt;</c>.
    /// </summary>
    private void WireAgentEventBridge(Infrastructure.EventBroker<Infrastructure.AgentEvent>? agentEventBus)
    {
        _agentEventBridge = new Infrastructure.AgentEventBridge(_mediator);

        // ── Presence detector ──
        if (_presenceDetector != null)
            _agentEventBridge.WirePresenceDetector(_presenceDetector);

        // ── Room voice events ──
        _agentEventBridge.WireRoomIntentBus();

        // ── ImmersivePersona consciousness + thought events ──
        if (_immersivePersona != null)
            _agentEventBridge.WirePersona(_immersivePersona);

        // ── Application-level Rx EventBus ──
        if (_serviceProvider != null)
        {
            var eventBus = _serviceProvider.GetService(typeof(Application.Integration.IEventBus))
                as Application.Integration.IEventBus;
            if (eventBus != null)
                _agentEventBridge.WireEventBus(eventBus);
        }

        // ── Start the agent event processing loop (creates _eventLoopCts) ──
        StartEventLoop();

        // ── CLI-level EventBroker<AgentEvent> (needs CTS from event loop) ──
        if (agentEventBus != null && _eventLoopCts != null)
            _agentEventBridge.WireAgentEventBroker(agentEventBus, _eventLoopCts.Token);

        _output.RecordInit("Agent Events", true, "MediatR notification pipeline active");
    }

    /// <summary>
    /// Wires the <see cref="AutonomousActionEngine"/> with the LLM and full self-awareness context,
    /// then starts its 3-minute loop. On by default — no config flag required.
    ///
    /// Self-awareness context passed on each cycle:
    ///   • Persona name + current mood/energy
    ///   • Active personality traits
    ///   • Recent conversation history (last 6 lines)
    ///   • Last autonomous thought
    /// </summary>
    private void WireAutonomousActionEngine()
    {
        if (_actionEngine is null) return;

        // ── ThinkFunction: use the same orchestrated model as autonomous mind ──
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

        // ── ExecuteFunc: full agent pipeline — pipe DSL, tool calls, everything ──
        _actionEngine.ExecuteFunc = async (command, ct) =>
        {
            try { return await ProcessInputWithPipingAsync(command); }
            catch (Exception ex) { return $"[Pipeline error: {ex.Message}]"; }
        };

        // ── GetAvailableToolsFunc: expose registered tool names ──
        _actionEngine.GetAvailableToolsFunc = () =>
            _tools.All.Select(t => t.Name).ToList();

        // ── GetContextFunc: rich self-awareness snapshot ──
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

        // ── OnAction: display as [Autonomous] 💬 with action + result, then persist ──
        _actionEngine.OnAction += async (reason, result) =>
        {
            if (string.IsNullOrWhiteSpace(reason)) return;

            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                AnsiConsole.MarkupLine(
                    $"\n  [bold][rgb(0,200,160)][Autonomous][/][/] 💬 {Markup.Escape(reason)}");

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

        // ── Start the loop ──
        _actionEngine.Start();
        _output.RecordInit("Autonomous Action Engine", true,
            $"started — interval: {_actionEngine.Interval.TotalMinutes:F0} min, full DSL access");
    }

    /// <summary>
    /// Wires AutonomousMind PipelineThinkFunction for monadic reasoning with branch tracking.

    private void WirePipelineThinkDelegate()
    {
        if (_autonomousMind == null) return;

        _autonomousMind.PipelineThinkFunction = async (prompt, existingBranch, token) =>
        {
            var response = await GenerateWithOrchestrationAsync(prompt, token);

            if (existingBranch != null)
            {
                var updatedBranch = existingBranch.WithReasoning(
                    new Ouroboros.Domain.States.Thinking(response),
                    prompt,
                    null);
                return (response, updatedBranch);
            }

            return (response, existingBranch!);
        };
    }

/// <summary>
    /// Enforce governance policies when self-modification is enabled.
    /// </summary>
    private async Task EnforceGovernancePoliciesAsync()
    {
        try
        {
            _output.WriteDebug("Enforcing governance policies...");

            var policyOpts = new PolicyOptions
            {
                Command = "enforce",
                Culture = _config.Culture,
                EnableSelfModification = true,
                RiskLevel = _config.RiskLevel,
                AutoApproveLow = _config.AutoApproveLow,
                Verbose = _config.Debug
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await PolicyCommands.RunPolicyAsync(policyOpts);
                    var output = writer.ToString();

                    Console.SetOut(originalOut);
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        _output.WriteDebug(output.Trim());
                    }
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"Policy enforcement: {ex.Message}");
        }
    }

    /// <summary>Gets the language name for a given culture code.</summary>

    private string GetLanguageName(string culture) => _localizationSub.GetLanguageName(culture);

    /// <summary>Gets the default Azure TTS voice name for a given culture code.</summary>
    private string GetDefaultVoiceForCulture(string? culture) =>
        _localizationSub.GetDefaultVoiceForCulture(culture);

    /// <summary>Gets the effective TTS voice, considering culture override.</summary>
    private string GetEffectiveVoice() => _localizationSub.GetEffectiveVoice();

    /// <summary>Translates a thought to the target language if culture is specified.</summary>
    private Task<string> TranslateThoughtIfNeededAsync(string thought) =>
        _localizationSub.TranslateThoughtIfNeededAsync(thought);
}