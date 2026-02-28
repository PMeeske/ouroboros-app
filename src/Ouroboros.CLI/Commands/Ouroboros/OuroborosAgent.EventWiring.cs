// <copyright file="OuroborosAgent.EventWiring.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Event-related cross-subsystem wiring: presence detection, persona events, system tools,
/// network persistence, avatar mood, cognitive streams, agent event bridge, pipeline think,
/// and governance policy enforcement.
/// </summary>
public sealed partial class OuroborosAgent
{
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

        // ImmersivePersona autonomous thoughts -- surface research/feelings, skip pure templates.
        // Metacognitive and Musing types come from InnerDialogEngine string templates
        // ("I notice that I tend to {0}") and are not genuine LLM thoughts -- skip those.
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

            // Push genuine persona thoughts to avatar -- excludes Metacognitive/Musing
            // templates which are filled from topic keywords, not LLM generation.
            if (_avatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy, svc.CurrentState.Positivity, statusText: content);
        };
    }

    /// <summary>
    /// Wires SystemAccessTools shared static state (SharedPersistence, SharedMind, SharedIndexer).
    /// </summary>
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
    /// </summary>
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

        // Presence detector
        if (_presenceDetector != null)
            _agentEventBridge.WirePresenceDetector(_presenceDetector);

        // Room voice events
        _agentEventBridge.WireRoomIntentBus();

        // ImmersivePersona consciousness + thought events
        if (_immersivePersona != null)
            _agentEventBridge.WirePersona(_immersivePersona);

        // Application-level Rx EventBus
        if (_serviceProvider != null)
        {
            var eventBus = _serviceProvider.GetService(typeof(Application.Integration.IEventBus))
                as Application.Integration.IEventBus;
            if (eventBus != null)
                _agentEventBridge.WireEventBus(eventBus);
        }

        // Start the agent event processing loop (creates _eventLoopCts)
        StartEventLoop();

        // CLI-level EventBroker<AgentEvent> (needs CTS from event loop)
        if (agentEventBus != null && _eventLoopCts != null)
            _agentEventBridge.WireAgentEventBroker(agentEventBus, _eventLoopCts.Token);

        _output.RecordInit("Agent Events", true, "MediatR notification pipeline active");
    }

    /// <summary>
    /// Creates the <see cref="Ouroboros.Application.Streams.CognitiveStreamEngine"/> and bridges
    /// all cognitive event sources into it as Rx streams. Adds interval-based pulse generators for
    /// valence (30 s) and personality (60 s). Wires the context block into ChatSubsystem and starts
    /// the throttled console display. No existing loops are modified.
    /// </summary>
    private void WireCognitiveStream()
    {
        _cognitiveStream = new Ouroboros.Application.Streams.CognitiveStreamEngine();

        // Bridge AutonomousMind events
        if (_autonomousMind != null)
        {
            _autonomousMind.OnThought         += t     => _cognitiveStream.EmitThought(t);
            _autonomousMind.OnDiscovery       += (q,f) => _cognitiveStream.EmitDiscovery(q, f);
            _autonomousMind.OnEmotionalChange += s     => _cognitiveStream.EmitEmotionalChange(s);
            _autonomousMind.OnAction          += a     => _cognitiveStream.EmitAutonomousAction(a);
        }

        // Bridge AutonomousActionEngine
        if (_actionEngine != null)
            _actionEngine.OnAction += (r, res) => _cognitiveStream.EmitActionEngine(r, res);

        // Bridge ImmersivePersona
        if (_immersivePersona != null)
        {
            _immersivePersona.AutonomousThought  += (_, e) => _cognitiveStream.EmitInnerDialog(e.Thought);
            _immersivePersona.ConsciousnessShift += (_, e) => _cognitiveStream.EmitConsciousnessShift(e.NewEmotion, e.ArousalChange);
        }

        // Bridge AutonomousCoordinator
        if (_autonomousCoordinator != null)
            _autonomousCoordinator.OnProactiveMessage += msg => _cognitiveStream.EmitCoordinatorMessage(msg);

        // Interval pulse generators
        if (_valenceMonitor != null)
            _cognitiveStream.StartValencePulse(_valenceMonitor);
        _cognitiveStream.StartPersonalityPulse(() => _personality);

        // Wire context block into ChatSubsystem
        _chatSub.CognitiveStreamEngine = _cognitiveStream;

        // OuroborosAtom events -> cognitive stream
        Ouroboros.Application.Tools.AutonomousTools.DefaultContext.CognitiveEmitFunc =
            _cognitiveStream.EmitRawThought;

        // Start throttled console display
        _cognitiveStream.StartConsoleDisplay(_config.Verbosity == OutputVerbosity.Quiet);

        _output.RecordInit("Cognitive Stream", true,
            "Rx thought streams active — all cognitive domains bridged");
    }

    /// <summary>
    /// Wires AutonomousMind PipelineThinkFunction for monadic reasoning with branch tracking.
    /// </summary>
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _output.WriteWarning($"Policy enforcement: {ex.Message}");
        }
    }
}
