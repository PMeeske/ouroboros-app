// <copyright file="OuroborosAgent.Persistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    /// <summary>
    /// Persists a new thought to storage for future sessions.
    /// Uses neuro-symbolic relations when Qdrant is available.
    /// </summary>
    private async Task PersistThoughtAsync(InnerThought thought, string? topic = null)
    {
        if (_thoughtPersistence == null) return;

        try
        {
            // Try to use neuro-symbolic persistence with automatic relation inference
            var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
            if (neuroStore != null)
            {
                var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
                var persisted = ToPersistedThought(thought, topic);
                await neuroStore.SaveWithRelationsAsync(sessionId, persisted, autoInferRelations: true);
            }
            else
            {
                await _thoughtPersistence.SaveAsync(thought, topic);
            }

            _persistentThoughts.Add(thought);

            // Keep only the most recent 100 thoughts in memory
            if (_persistentThoughts.Count > 100)
            {
                _persistentThoughts.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the result of a thought execution (action taken, response generated, etc).
    /// </summary>

    private async Task PersistThoughtResultAsync(
        Guid thoughtId,
        string resultType,
        string content,
        bool success,
        double confidence,
        TimeSpan? executionTime = null)
    {
        if (_thoughtPersistence == null) return;

        var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
        if (neuroStore == null) return;

        try
        {
            var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
            var result = new Ouroboros.Domain.Persistence.ThoughtResult(
                Id: Guid.NewGuid(),
                ThoughtId: thoughtId,
                ResultType: resultType,
                Content: content,
                Success: success,
                Confidence: confidence,
                CreatedAt: DateTime.UtcNow,
                ExecutionTime: executionTime);

            await neuroStore.SaveResultAsync(sessionId, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtResult] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an InnerThought to a PersistedThought.
    /// </summary>

    private static Ouroboros.Domain.Persistence.PersistedThought ToPersistedThought(InnerThought thought, string? topic)
    {
        string? metadataJson = null;
        if (thought.Metadata != null && thought.Metadata.Count > 0)
        {
            try
            {
                metadataJson = System.Text.Json.JsonSerializer.Serialize(thought.Metadata);
            }
            catch
            {
                // Ignore
            }
        }

        return new Ouroboros.Domain.Persistence.PersistedThought
        {
            Id = thought.Id,
            Type = thought.Type.ToString(),
            Content = thought.Content,
            Confidence = thought.Confidence,
            Relevance = thought.Relevance,
            Timestamp = thought.Timestamp,
            Origin = thought.Origin.ToString(),
            Priority = thought.Priority.ToString(),
            ParentThoughtId = thought.ParentThoughtId,
            TriggeringTrait = thought.TriggeringTrait,
            Topic = topic,
            Tags = thought.Tags,
            MetadataJson = metadataJson,
        };
    }


    /// <summary>
    /// Handles presence detection - greets user proactively if push mode enabled.

    private async Task HandlePresenceDetectedAsync(PresenceEvent evt)
    {
        System.Diagnostics.Debug.WriteLine($"[Presence] User presence detected via {evt.Source} (confidence={evt.Confidence:P0})");

        // Only proactively greet if:
        // 1. Push mode is enabled
        // 2. User was previously absent (state changed)
        // 3. Haven't greeted recently (avoid spam)
        var shouldGreet = _config.EnablePush &&
                          !_userWasPresent &&
                          (DateTime.UtcNow - _lastGreetingTime).TotalMinutes > 5 &&
                          evt.Confidence > 0.6;

        _userWasPresent = true;

        if (shouldGreet)
        {
            _lastGreetingTime = DateTime.UtcNow;

            // Generate a contextual greeting
            var greeting = await GeneratePresenceGreetingAsync(evt);

            // Notify via AutonomousMind's proactive channel
            if (_autonomousMind != null && !_autonomousMind.SuppressProactiveMessages)
            {
                // Fire proactive message event
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  👋 {greeting}");
                Console.ResetColor();

                // Speak the greeting
                await _voice.WhisperAsync(greeting);

                // If in conversation loop, restore prompt
                if (_isInConversationLoop)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n  You: ");
                    Console.ResetColor();
                }
            }
        }
    }

    /// <summary>
    /// Generates a contextual greeting when user presence is detected.
    /// </summary>

    private async Task<string> GeneratePresenceGreetingAsync(PresenceEvent evt)
    {
        var defaultGreeting = GetLocalizedString("Welcome back! I'm here if you need anything.");

        if (_chatModel == null)
        {
            return defaultGreeting;
        }

        try
        {
            var context = evt.TimeSinceLastState.HasValue
                ? $"The user was away for {evt.TimeSinceLastState.Value.TotalMinutes:F0} minutes."
                : "The user just arrived.";

            // Add language directive if culture is set
            var languageDirective = GetLanguageDirective();

            var prompt = PromptResources.GreetingGeneration(languageDirective, context);

            var greeting = await _chatModel.GenerateTextAsync(prompt, CancellationToken.None);
            return greeting?.Trim() ?? defaultGreeting;
        }
        catch
        {
            return defaultGreeting;
        }
    }

    /// <summary>
    /// Performs AGI warmup at startup - primes the model with examples for autonomous operation.
    /// </summary>

    private async Task PerformAgiWarmupAsync()
    {
        try
        {
            if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n  ⏳ Warming up AGI systems...");
                Console.ResetColor();
            }

            _agiWarmup = new AgiWarmup(
                thinkFunction: _autonomousMind?.ThinkFunction,
                searchFunction: _autonomousMind?.SearchFunction,
                executeToolFunction: _autonomousMind?.ExecuteToolFunction,
                selfIndexer: _selfIndexer,
                toolRegistry: _tools);

            if (_autonomousMind != null)
            {
                _autonomousMind.Config.ThinkingIntervalSeconds = 15;
            }

            if (_config.Verbosity == OutputVerbosity.Verbose)
            {
                _agiWarmup.OnProgress += (step, percent) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\r  ⏳ {step} ({percent}%)".PadRight(60));
                    Console.ResetColor();
                };
            }

            var result = await _agiWarmup.WarmupAsync();

            if (_config.Verbosity == OutputVerbosity.Verbose)
            {
                Console.WriteLine(); // Clear progress line

                if (result.Success)
                    _output.WriteDebug($"AGI warmup complete in {result.Duration.TotalSeconds:F1}s");
                else
                    _output.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }
            else if (_config.Verbosity != OutputVerbosity.Quiet)
            {
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  ✓ Autonomous mind active");
                    Console.ResetColor();
                }
                else
                    _output.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }

            // Warmup thought seeded into curiosity queue rather than displayed (shifts with conversation)
            if (result.Success && !string.IsNullOrEmpty(result.WarmupThought))
            {
                _autonomousMind?.InjectTopic(result.WarmupThought);
            }

            // Trigger Scrutor assembly scan now that all subsystems are registered —
            // discovers all ITool implementations and builds the IServiceProvider.
            _ = Ouroboros.Application.Tools.ServiceContainerFactory.Build();
        }
        catch (Exception ex)
        {
            _output.WriteWarning($"AGI warmup skipped: {ex.Message}");
        }
    }

    // ── SelfAssembly (delegated to SelfAssemblySubsystem) ────────────────────

    /// <summary>Analyzes capability gaps and proposes new neurons.</summary>

    // ── SelfAssembly (delegated to SelfAssemblySubsystem) ────────────────────

    /// <summary>Analyzes capability gaps and proposes new neurons.</summary>
    public Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
        => _selfAssemblySub.AnalyzeAndProposeNeuronsAsync(ct);

    /// <summary>Attempts to assemble a neuron from a blueprint.</summary>
    public Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
        => _selfAssemblySub.AssembleNeuronAsync(blueprint, ct);
}
