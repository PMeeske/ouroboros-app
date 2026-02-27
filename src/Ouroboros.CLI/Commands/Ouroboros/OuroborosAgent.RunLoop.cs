// <copyright file="OuroborosAgent.RunLoop.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Mediator;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    private void PrintFeatureStatus()
    {
        AnsiConsole.MarkupLine(Markup.Escape("  Configuration:"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Model: {_config.Model}"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Persona: {_config.Persona}"));
        var ttsMode = _config.AzureTts ? "✓ Azure (cloud)" : "○ Local (Windows)";
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"    Voice: {(_config.Voice ? "✓ enabled" : "○ disabled")} - {ttsMode}"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(Markup.Escape("  Features (all enabled by default, use --no-X to disable):"));
        PrintFeatureLine(_config.EnableSkills, "Skills       - Persistent learning with Qdrant");
        PrintFeatureLine(_config.EnableMeTTa, "MeTTa        - Symbolic reasoning engine");
        PrintFeatureLine(_config.EnableTools, "Tools        - Web search, calculator, URL fetch");
        PrintFeatureLine(_config.EnableBrowser, "Browser      - Playwright automation");
        PrintFeatureLine(_config.EnablePersonality, "Personality  - Affective states & traits");
        PrintFeatureLine(_config.EnableMind, "Mind         - Autonomous inner thoughts");
        PrintFeatureLine(_config.EnableConsciousness, "Consciousness- ImmersivePersona self-awareness");
        PrintFeatureLine(_config.EnableEmbodiment, "Embodiment   - Multimodal sensors & actuators");
        AnsiConsole.MarkupLine(_config.EnablePush
            ? $"[rgb(148,103,189)]    {Markup.Escape("⚡ Push Mode    - Propose actions for approval (--push)")}[/]"
            : OuroborosTheme.Dim("    ○ Push Mode    - Propose actions for approval (--push)"));
        AnsiConsole.WriteLine();

        static void PrintFeatureLine(bool enabled, string description)
        {
            var symbol = enabled ? "✓" : "○";
            var line = $"    {symbol} {description}";
            AnsiConsole.MarkupLine(enabled ? OuroborosTheme.Ok(line) : OuroborosTheme.Dim(line));
        }
    }

    private void PrintQuickHelp()
    {
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  Quick commands: 'help' | 'status' | 'skills' | 'tools' | 'exit'"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("  Say or type anything to chat. Use \\[TOOL:name args] to call tools."));
        AnsiConsole.WriteLine();
    }

    public async Task RunAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Handle pipe/batch/exec modes
        if (_config.PipeMode || !string.IsNullOrWhiteSpace(_config.BatchFile) || !string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            await RunNonInteractiveModeAsync();
            return;
        }

        if (_config.Verbosity == OutputVerbosity.Verbose)
            _voice.PrintHeader("OUROBOROS");

        // Greeting - let the LLM generate a natural Cortana-like greeting
        if (!_config.NoGreeting)
        {
            var greeting = await GetGreetingAsync();
            await SayWithVoiceAsync(greeting);
        }

        _isInConversationLoop = true;
        bool running = true;
        int interactionsSinceSnapshot = 0;
        while (running)
        {
            var input = await GetInputWithVoiceAsync("\n  You: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Notify cognitive stream — interleaves user interaction with running streams
            _cognitiveStream?.EmitUserInteraction(input);

            // Track conversation
            _conversationHistory.Add($"User: {input}");
            interactionsSinceSnapshot++;

            // Feed to autonomous coordinator for topic discovery
            _autonomousCoordinator?.AddConversationContext($"User: {input}");

            // Shift autonomous mind's curiosity toward what's being discussed
            _autonomousMind?.InjectTopic(input);

            // Check for exit
            if (IsExitCommand(input))
            {
                await SayWithVoiceAsync(GetLocalizedString("Until next time! I'll keep learning while you're away."));
                running = false;
                continue;
            }

            // Process input through the agent (with pipe support)
            try
            {
                var response = await ProcessInputWithPipingAsync(input);

                // Display cost info after each response if enabled
                if (_config.ShowCosts && _costTracker != null)
                {
                    var costString = _costTracker.GetCostString();
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim($"  [{costString}]"));
                }

                // Strip tool results for voice output (full response shown in console)
                var voiceResponse = StripToolResults(response);
                if (!string.IsNullOrWhiteSpace(voiceResponse))
                {
                    await SayWithVoiceAsync(voiceResponse);
                }

                // Also speak on side channel if enabled (non-blocking)
                Say(response);

                _conversationHistory.Add($"Ouroboros: {response}");

                // Feed response to coordinator too
                _autonomousCoordinator?.AddConversationContext($"Ouroboros: {response[..Math.Min(200, response.Length)]}");

                // Periodic personality snapshot every 10 interactions
                if (interactionsSinceSnapshot >= 10 && _personalityEngine != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _personalityEngine.SavePersonalitySnapshotAsync(_voice.ActivePersona.Name);
                            System.Diagnostics.Debug.WriteLine("[Personality] Periodic snapshot saved");
                        }
                        catch (Exception) { /* Ignore — snapshot is non-critical */ }
                    });
                    interactionsSinceSnapshot = 0;
                }
            }
            catch (Exception ex)
            {
                await SayWithVoiceAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Speaks text using the unified voice service with Rx streaming and Cortana-style voice.
    /// Delegates to <see cref="SayWithVoiceHandler"/> via MediatR.
    /// </summary>
    /// <param name="text">The text to speak.</param>

    private Task SayWithVoiceAsync(string text, CancellationToken ct = default, bool isWhisper = false)
        => _mediator.Send(new SayWithVoiceRequest(text, isWhisper), ct);

    /// <summary>
    /// Speaks an inner thought using soft whispering style.
    /// </summary>
    /// <param name="thought">The thought to speak.</param>

    private Task SayThoughtWithVoiceAsync(string thought, CancellationToken ct = default)
        => SayWithVoiceAsync(thought, ct, isWhisper: true);

    private Task<string> GetInputWithVoiceAsync(string prompt, CancellationToken ct = default)
        => _mediator.Send(new GetInputWithVoiceRequest(prompt), ct);

    // ── Pipe processing (delegated to PipeProcessingSubsystem) ─────────────────

    /// <summary>Runs in non-interactive mode for piping, batch processing, or single command execution.</summary>
    private Task RunNonInteractiveModeAsync() => _pipeSub.RunNonInteractiveModeAsync();

    /// <summary>Processes input with support for | piping syntax. Delegates to <see cref="ProcessInputPipingHandler"/> via MediatR.</summary>
    public Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5)
        => _mediator.Send(new ProcessInputPipingRequest(input, maxPipeDepth));

    /// <summary>
    /// Processes user input and returns a response.

    public async Task<string> ProcessInputAsync(string input)
    {
        // Parse for action commands
        var action = _commandRoutingSub.ParseAction(input);

        return action.Type switch
        {
            ActionType.Help => _commandRoutingSub.GetHelpText(),
            ActionType.ListSkills => await ListSkillsAsync(),
            ActionType.ListTools => _commandRoutingSub.ListTools(),
            ActionType.LearnTopic => await LearnTopicAsync(action.Argument),
            ActionType.CreateTool => await CreateToolAsync(action.Argument),
            ActionType.UseTool => await UseToolAsync(action.Argument, action.ToolInput),
            ActionType.RunSkill => await RunSkillAsync(action.Argument),
            ActionType.Suggest => await SuggestSkillsAsync(action.Argument),
            ActionType.Plan => await PlanAsync(action.Argument),
            ActionType.Execute => await ExecuteAsync(action.Argument),
            ActionType.Status => _commandRoutingSub.GetStatus(),
            ActionType.Mood => _commandRoutingSub.GetMood(),
            ActionType.Remember => await RememberAsync(action.Argument),
            ActionType.Recall => await RecallAsync(action.Argument),
            ActionType.Query => await QueryMeTTaAsync(action.Argument),
            // Unified CLI commands
            ActionType.Ask => await AskAsync(action.Argument),
            ActionType.Pipeline => await RunPipelineAsync(action.Argument),
            ActionType.Metta => await RunMeTTaExpressionAsync(action.Argument),
            ActionType.Orchestrate => await OrchestrateAsync(action.Argument),
            ActionType.Network => await NetworkCommandAsync(action.Argument),
            ActionType.Dag => await DagCommandAsync(action.Argument),
            ActionType.Affect => await AffectCommandAsync(action.Argument),
            ActionType.Environment => await EnvironmentCommandAsync(action.Argument),
            ActionType.Maintenance => await MaintenanceCommandAsync(action.Argument),
            ActionType.Policy => await PolicyCommandAsync(action.Argument),
            ActionType.Explain => _commandRoutingSub.ExplainDsl(action.Argument),
            ActionType.Test => await RunTestAsync(action.Argument),
            // Merged from ImmersiveMode and Skills mode
            ActionType.Consciousness => GetConsciousnessState(),
            ActionType.Tokens => _commandRoutingSub.GetDslTokens(),
            ActionType.Fetch => await FetchResearchAsync(action.Argument),
            ActionType.Process => await ProcessLargeInputAsync(action.Argument),
            // Self-execution and sub-agent commands
            ActionType.SelfExec => await SelfExecCommandAsync(action.Argument),
            ActionType.SubAgent => await SubAgentCommandAsync(action.Argument),
            ActionType.Epic => await EpicCommandAsync(action.Argument),
            ActionType.Goal => await GoalCommandAsync(action.Argument),
            ActionType.Delegate => await DelegateCommandAsync(action.Argument),
            ActionType.SelfModel => await SelfModelCommandAsync(action.Argument),
            ActionType.Evaluate => await EvaluateCommandAsync(action.Argument),
            // Emergent behavior commands
            ActionType.Emergence => await EmergenceCommandAsync(action.Argument),
            ActionType.Dream => await DreamCommandAsync(action.Argument),
            ActionType.Introspect => await IntrospectCommandAsync(action.Argument),
            // Push mode commands
            ActionType.Approve => await ApproveIntentionAsync(action.Argument),
            ActionType.Reject => await RejectIntentionAsync(action.Argument),
            ActionType.Pending => ListPendingIntentions(),
            ActionType.PushPause => PausePushMode(),
            ActionType.PushResume => ResumePushMode(),
            ActionType.CoordinatorCommand => _commandRoutingSub.ProcessCoordinatorCommand(input),
            // Self-modification commands (direct tool invocation)
            ActionType.SaveCode => await SaveCodeCommandAsync(action.Argument),
            ActionType.SaveThought => await SaveThoughtCommandAsync(action.Argument),
            ActionType.ReadMyCode => await ReadMyCodeCommandAsync(action.Argument),
            ActionType.SearchMyCode => await SearchMyCodeCommandAsync(action.Argument),
            ActionType.AnalyzeCode => await AnalyzeCodeCommandAsync(action.Argument),
            // Index commands
            ActionType.Reindex => await ReindexFullAsync(),
            ActionType.ReindexIncremental => await ReindexIncrementalAsync(),
            ActionType.IndexSearch => await IndexSearchAsync(action.Argument),
            ActionType.IndexStats => await GetIndexStatsAsync(),
            // AGI subsystem commands
            ActionType.AgiStatus => GetAgiStatus(),
            ActionType.AgiCouncil => await RunCouncilDebateAsync(action.Argument),
            ActionType.AgiIntrospect => GetIntrospectionReport(),
            ActionType.AgiWorld => GetWorldModelStatus(),
            ActionType.AgiCoordinate => await RunAgentCoordinationAsync(action.Argument),
            ActionType.AgiExperience => GetExperienceBufferStatus(),
            ActionType.PromptOptimize => GetPromptOptimizerStatus(),
            ActionType.Chat => await ChatAsync(input),
            _ => await ChatAsync(input)
        };
    }

    private Task<string> GetGreetingAsync()
        => _mediator.Send(new GetGreetingRequest());

    private string GetLocalizedTimeOfDay(int hour) => _localizationSub.GetLocalizedTimeOfDay(hour);
    private string[] GetLocalizedFallbackGreetings(string timeOfDay) => _localizationSub.GetLocalizedFallbackGreetings(timeOfDay);
    private string GetLocalizedString(string key) => _localizationSub.GetLocalizedString(key);
    private string GetLanguageDirective() => _localizationSub.GetLanguageDirective();
}