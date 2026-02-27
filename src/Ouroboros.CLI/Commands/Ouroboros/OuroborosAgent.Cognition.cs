// <copyright file="OuroborosAgent.Cognition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Ouroboros.Application.Configuration;
using Ouroboros.CLI.Mediator;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    private string GetConsciousnessState()
        => ((CognitiveSubsystem)_cognitiveSub).GetConsciousnessState();

    public async Task RunAsync(CancellationToken ct = default)
    {
        // 1. Create ImmersiveMode — Iaret's interactive face
        var immersive = new ImmersiveMode(
            _modelsSub, _toolsSub, _memorySub, _autonomySub,
            _immersivePersona, AvatarService, _serviceProvider);

        // 2. Create RoomMode — ambient presence
        var room = new RoomMode(immersive, _modelsSub, _memorySub, _autonomySub, _serviceProvider);

        // 3. Wire RoomIntentBus → ImmersiveMode (room events appear in the chat pane)
        Ouroboros.CLI.Services.RoomPresence.RoomIntentBus.Reset();
        Ouroboros.CLI.Services.RoomPresence.RoomIntentBus.OnIaretInterjected   += immersive.ShowRoomInterjection;
        Ouroboros.CLI.Services.RoomPresence.RoomIntentBus.OnUserAddressedIaret += immersive.ShowRoomAddress;

        // 4. Build voice/TTS options from agent config
        var opts = new Ouroboros.Options.ImmersiveCommandVoiceOptions
        {
            Persona           = _config.Persona ?? "Iaret",
            Model             = _config.Model   ?? "deepseek-v3.1:671b-cloud",
            Endpoint          = _config.Endpoint ?? DefaultEndpoints.Ollama,
            EmbedModel        = _config.EmbedModel ?? "nomic-embed-text",
            QdrantEndpoint    = _config.QdrantEndpoint ?? DefaultEndpoints.QdrantGrpc,
            Voice             = _config.Voice,
            VoiceOnly         = _config.VoiceOnly,
            LocalTts          = _config.LocalTts,
            AzureTts          = _config.AzureTts,
            TtsVoice          = _config.TtsVoice ?? "en-US-JennyMultilingualNeural",
            AzureSpeechKey    = _config.AzureSpeechKey,
            AzureSpeechRegion = _config.AzureSpeechRegion ?? "eastus",
            Avatar            = true,
            AvatarPort        = 9471,
            RoomMode          = false,
        };

        // 5. Start RoomMode in background
        using var roomCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var roomTask = Task.Run(async () =>
        {
            try
            {
                await room.RunAsync(
                    personaName       : opts.Persona,
                    model             : opts.Model,
                    endpoint          : opts.Endpoint,
                    embedModel        : opts.EmbedModel,
                    qdrant            : opts.QdrantEndpoint,
                    azureSpeechKey    : _config.AzureSpeechKey,
                    azureSpeechRegion : _config.AzureSpeechRegion ?? "eastus",
                    ttsVoice          : _config.TtsVoice ?? "en-US-JennyMultilingualNeural",
                    localTts          : _config.LocalTts,
                    avatarOn          : false,
                    avatarPort        : 9471,
                    quiet             : true,
                    ct                : roomCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomMode] exited: {ex.Message}");
            }
        }, roomCts.Token);

        // 6. Run ImmersiveMode in foreground (Iaret's interactive face)
        try
        {
            await immersive.RunAsync(opts, ct);
        }
        finally
        {
            Ouroboros.CLI.Services.RoomPresence.RoomIntentBus.Reset();
            await roomCts.CancelAsync();
            try { await roomTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception) { /* best-effort room shutdown */ }
        }
    }

    /// <inheritdoc/>

    private Task<string> GenerateWithOrchestrationAsync(string prompt, CancellationToken ct = default)
        => _mediator.Send(new OrchestrationRequest(prompt), ct);

    /// <summary>
    /// Processes large text input using divide-and-conquer parallel processing.
    /// Automatically chunks the input, processes in parallel, and merges results.
    /// </summary>
    /// <param name="task">The task instruction (e.g., "Summarize:", "Analyze:", "Extract key points:")</param>
    /// <param name="largeInput">The large text input to process</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Merged result from all chunk processing</returns>
    public Task<string> ProcessLargeInputAsync(string task, string largeInput, CancellationToken ct = default)
        => _mediator.Send(new ProcessLargeInputRequest(task, largeInput), ct);

    /// <summary>
    /// Gets the current orchestration metrics showing model usage statistics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics>? GetOrchestrationMetrics()
    {
        if (_orchestratedModel != null)
        {
            // Access through the builder's underlying orchestrator
            return null; // Would need to expose metrics from OrchestratedChatModel
        }

        return _divideAndConquer?.GetMetrics();
    }

    /// <summary>
    /// Checks if multi-model orchestration is enabled and available.
    /// </summary>
    public bool IsMultiModelEnabled => _orchestratedModel != null;

    /// <summary>
    /// Checks if divide-and-conquer processing is available.
    /// </summary>
    public bool IsDivideAndConquerEnabled => _divideAndConquer != null;

    //
    // AUTONOMY DELEGATES (methods moved to AutonomySubsystem)
    //

    private Task InitializeAutonomousCoordinatorAsync()
        => _autonomySub.InitializeAutonomousCoordinatorAsync();

    private Task<string> SelfExecCommandAsync(string subCommand)
        => _autonomySub.SelfExecCommandAsync(subCommand);

    private Task<string> SubAgentCommandAsync(string subCommand)
        => _autonomySub.SubAgentCommandAsync(subCommand);

    private Task<string> EpicCommandAsync(string subCommand)
        => _autonomySub.EpicCommandAsync(subCommand);

    private Task<string> GoalCommandAsync(string subCommand)
        => _autonomySub.GoalCommandAsync(subCommand);

    private Task<string> DelegateCommandAsync(string taskDescription)
        => _autonomySub.DelegateCommandAsync(taskDescription);

    private Task<string> SelfModelCommandAsync(string subCommand)
        => _autonomySub.SelfModelCommandAsync(subCommand);

    private Task<string> EvaluateCommandAsync(string subCommand)
        => _autonomySub.EvaluateCommandAsync(subCommand);

    // Push Mode commands (moved to AutonomySubsystem)
    private Task<string> ApproveIntentionAsync(string arg)
        => _autonomySub.ApproveIntentionAsync(arg);

    private Task<string> RejectIntentionAsync(string arg)
        => _autonomySub.RejectIntentionAsync(arg);

    private string ListPendingIntentions()
        => _autonomySub.ListPendingIntentions();

    private string PausePushMode()
        => _autonomySub.PausePushMode();

    private string ResumePushMode()
        => _autonomySub.ResumePushMode();

    //
    //  COGNITIVE DELEGATES  Emergent Behavior Commands (logic in CognitiveSubsystem)
    //

    private Task<string> EmergenceCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).EmergenceCommandAsync(topic);

    private Task<string> DreamCommandAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).DreamCommandAsync(topic);

    private Task<string> IntrospectCommandAsync(string focus)
        => ((CognitiveSubsystem)_cognitiveSub).IntrospectCommandAsync(focus);

    // Thought commands (moved to MemorySubsystem)
    private Task<string> SaveThoughtCommandAsync(string argument)
        => _memorySub.SaveThoughtCommandAsync(argument);

    private void TrackLastThought(string content)
        => _memorySub.TrackLastThought(content);

    // Code Self-Perception commands (moved to AutonomySubsystem)
    private Task<string> SaveCodeCommandAsync(string argument)
        => _autonomySub.SaveCodeCommandAsync(argument);

    private Task<string> ReadMyCodeCommandAsync(string filePath)
        => _autonomySub.ReadMyCodeCommandAsync(filePath);

    private Task<string> SearchMyCodeCommandAsync(string query)
        => _autonomySub.SearchMyCodeCommandAsync(query);

    private Task<string> AnalyzeCodeCommandAsync(string input)
        => _autonomySub.AnalyzeCodeCommandAsync(input);

    // Index commands (moved to AutonomySubsystem)
    private Task<string> ReindexFullAsync()
        => _autonomySub.ReindexFullAsync();

    private Task<string> ReindexIncrementalAsync()
        => _autonomySub.ReindexIncrementalAsync();

    private Task<string> IndexSearchAsync(string query)
        => _autonomySub.IndexSearchAsync(query);

    private Task<string> GetIndexStatsAsync()
        => _autonomySub.GetIndexStatsAsync();
    //
    //  COGNITIVE DELEGATES  AGI Subsystem Methods (logic in CognitiveSubsystem)
    //

    private void RecordInteractionForLearning(string input, string response)
        => ((CognitiveSubsystem)_cognitiveSub).RecordInteractionForLearning(input, response);

    private void RecordCognitiveEvent(string input, string response, List<ToolExecution>? tools)
        => ((CognitiveSubsystem)_cognitiveSub).RecordCognitiveEvent(input, response, tools);

    private void UpdateSelfAssessment(string input, string response, List<ToolExecution>? tools)
        => ((CognitiveSubsystem)_cognitiveSub).UpdateSelfAssessment(input, response, tools);

    private string GetAgiStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetAgiStatus();

    private Task<string> RunCouncilDebateAsync(string topic)
        => ((CognitiveSubsystem)_cognitiveSub).RunCouncilDebateAsync(topic);

    private string GetIntrospectionReport()
        => ((CognitiveSubsystem)_cognitiveSub).GetIntrospectionReport();

    private string GetWorldModelStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetWorldModelStatus();

    private Task<string> RunAgentCoordinationAsync(string goalDescription)
        => ((CognitiveSubsystem)_cognitiveSub).RunAgentCoordinationAsync(goalDescription);

    private string GetExperienceBufferStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetExperienceBufferStatus();

    private string GetPromptOptimizerStatus()
        => ((CognitiveSubsystem)_cognitiveSub).GetPromptOptimizerStatus();

    private static string TruncateText(string text, int maxLength)
        => CognitiveSubsystem.TruncateText(text, maxLength);

}