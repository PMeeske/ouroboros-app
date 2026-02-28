// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Abstractions.Agent;
using Ouroboros.Application.Extensions;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Core.Configuration;
using Ouroboros.CLI.Commands;
using Ouroboros.Network;
using Spectre.Console;
using Unit = Ouroboros.Abstractions.Unit;
using static Ouroboros.Application.Tools.AutonomousTools;

/// <summary>
/// Partial: Autonomous coordinator initialization, push mode loop, event handlers,
/// and intention approval/rejection commands.
/// </summary>
public sealed partial class AutonomySubsystem
{
    /// <summary>
    /// Initializes the autonomous coordinator (always enabled for status, commands, network).
    /// </summary>
    internal async Task InitializeAutonomousCoordinatorAsync()
    {
        try
        {
            // Parse auto-approve categories from config
            HashSet<string> autoApproveCategories = Config.AutoApproveCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Create autonomous configuration using existing API
            AutonomousConfiguration autonomousConfig = new AutonomousConfiguration
            {
                PushBasedMode = Config.EnablePush,
                YoloMode = Config.YoloMode,
                TickIntervalSeconds = Config.IntentionIntervalSeconds,
                AutoApproveLowRisk = autoApproveCategories.Contains("safe") || autoApproveCategories.Contains("low"),
                AutoApproveMemoryOps = autoApproveCategories.Contains("memory"),
                AutoApproveSelfReflection = autoApproveCategories.Contains("analysis") || autoApproveCategories.Contains("reflection"),
                EnableProactiveCommunication = Config.EnablePush,
                EnableCodeModification = !autoApproveCategories.Contains("no-code"),
                Culture = Config.Culture
            };

            // Create the autonomous coordinator
            Coordinator = new AutonomousCoordinator(autonomousConfig);

            // Share coordinator with autonomous tools (enables status checks even without push mode)
            Ouroboros.Application.Tools.AutonomousTools.DefaultContext.Coordinator = Coordinator;

            // Wire up event handlers — use fire-and-forget with exception observation
            // because the event delegate signature is synchronous (Action<T>)
            Coordinator.OnProactiveMessage += args =>
                HandleAutonomousMessageAsync(args).ObserveExceptions("HandleAutonomousMessage");
            Coordinator.OnIntentionRequiresAttention += HandleIntentionAttention;

            // Configure functions if available
            if (Models.Llm != null)
            {
                Coordinator.ExecuteToolFunction = async (tool, args, ct) =>
                {
                    ITool? toolObj = Tools.Tools.All.FirstOrDefault(t => t.Name == tool);
                    if (toolObj != null)
                    {
                        Result<string, string> result = await toolObj.InvokeAsync(args, ct);
                        return result.Match(
                            success => success,
                            error => $"Tool execution failed: {error}");
                    }
                    return $"Tool '{tool}' not found.";
                };

                // Wire up ThinkFunction for autonomous topic discovery
                Coordinator.ThinkFunction = async (prompt, ct) =>
                {
                    (string response, List<ToolExecution> _) = await Models.Llm.GenerateWithToolsAsync(prompt, ct);
                    return response;
                };
            }

            if (Models.Embedding != null)
            {
                Coordinator.EmbedFunction = async (text, ct) =>
                {
                    return await Models.Embedding.CreateEmbeddingsAsync(text, ct);
                };
            }

            // Wire up Qdrant storage and search for autonomous memory
            if (Memory.NeuralMemory != null)
            {
                Coordinator.StoreToQdrantFunction = async (category, content, embedding, ct) =>
                {
                    await Memory.NeuralMemory.StoreMemoryAsync(category, content, embedding, ct);
                };

                Coordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    return await Memory.NeuralMemory.SearchMemoriesAsync(embedding, limit, ct);
                };

                // Wire up intention storage
                Coordinator.StoreIntentionFunction = async (intention, ct) =>
                {
                    await Memory.NeuralMemory.StoreIntentionAsync(intention, ct);
                };

                // Wire up neuron message storage
                Coordinator.StoreNeuronMessageFunction = async (message, ct) =>
                {
                    await Memory.NeuralMemory.StoreNeuronMessageAsync(message, ct);
                };
            }
            else if (Memory.Skills != null)
            {
                // Fallback: Use skills to find related context
                Coordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    IEnumerable<Skill> results = await Memory.Skills.FindMatchingSkillsAsync("recent topics and interests", null);
                    return results.Take(limit).Select(s => $"{s.Name}: {s.Description}").ToList();
                };
            }

            // Wire up MeTTa symbolic reasoning functions
            if (Memory.MeTTaEngine != null)
            {
                Coordinator.MeTTaQueryFunction = async (query, ct) =>
                {
                    Result<string, string> result = await Memory.MeTTaEngine.ExecuteQueryAsync(query, ct);
                    return result.Match(
                        success => success,
                        error => $"MeTTa error: {error}");
                };

                Coordinator.MeTTaAddFactFunction = async (fact, ct) =>
                {
                    Result<Unit, string> result = await Memory.MeTTaEngine.AddFactAsync(fact, ct);
                    return result.IsSuccess;
                };

                // Wire up DAG constraint verification through NetworkTracker
                if (NetworkTracker?.HasMeTTaEngine == true)
                {
                    Coordinator.VerifyDagConstraintFunction = async (branchName, constraint, ct) =>
                    {
                        Result<bool> result = await NetworkTracker.VerifyConstraintAsync(branchName, constraint, ct);
                        return result.IsSuccess && result.Value;
                    };
                }
            }

            // Wire up ProcessChatFunction for auto-training mode
            Coordinator.ProcessChatFunction = async (message, ct) =>
            {
                // Process through the main chat pipeline and return response
                string response = await ChatAsyncFunc(message);
                return response;
            };

            // Wire up FullChatWithToolsFunction for User persona in problem-solving mode
            Coordinator.FullChatWithToolsFunction = async (message, ct) =>
            {
                string response = await ChatAsyncFunc(message);
                return response;
            };

            // Wire up DisplayAndSpeakFunction for proper User->Ouroboros sequencing
            Coordinator.DisplayAndSpeakFunction = async (message, persona, ct) =>
            {
                bool isUser = persona == "User";
                var color = isUser ? OuroborosTheme.Warn($"\n  {message}") : $"[rgb(148,103,189)]{Markup.Escape($"\n  {message}")}[/]";
                AnsiConsole.MarkupLine(color);

                await SayAndWaitAsyncFunc(message, persona);
            };

            // Wire up proactive message suppression for problem-solving mode
            Coordinator.SetSuppressProactiveMessages = (suppress) =>
            {
                if (AutonomousMind != null)
                {
                    AutonomousMind.SuppressProactiveMessages = suppress;
                }
            };

            // Wire up voice output (TTS) toggle
            Coordinator.SetVoiceEnabled = (enabled) =>
            {
                if (Voice.SideChannel != null)
                {
                    Voice.SideChannel.SetEnabled(enabled);
                }
            };

            // Wire up voice input (STT) toggle
            Coordinator.SetListeningEnabled = (enabled) =>
            {
                if (enabled)
                {
                    StartListeningAsyncFunc().ConfigureAwait(false);
                }
                else
                {
                    StopListeningAsyncAction().ConfigureAwait(false);
                }
            };

            // Configure topic discovery interval
            Coordinator.TopicDiscoveryIntervalSeconds = Config.DiscoveryIntervalSeconds;

            // Populate available tools for priority resolution
            Coordinator.AvailableTools = Tools.Tools.All.Select(t => t.Name).ToHashSet();

            // Start the neural network (for status visibility) without coordination loops
            Coordinator.StartNetwork();

            Output.RecordInit("Coordinator", true, "neural network active");
            await Task.CompletedTask;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Warn($"⚠ Autonomous Coordinator initialization failed: {ex.Message}")}");
        }
    }

    /// <summary>
    /// Handles proactive messages from autonomous coordinator.
    /// Must not be called directly from event handlers — use fire-and-forget via ObserveExceptions.
    /// </summary>
    internal async Task HandleAutonomousMessageAsync(ProactiveMessageEventArgs args)
    {
        // Always show auto-training and user_persona messages
        bool isTrainingMessage = args.Source is "user_persona" or "auto_training";

        // Skip non-training messages during conversation loop to avoid cluttering
        if (IsInConversationLoop() && !isTrainingMessage && args.Priority < IntentionPriority.High)
            return;

        // In Normal mode, only show training and high-priority messages
        if (Config.Verbosity < OutputVerbosity.Verbose && !isTrainingMessage && args.Priority < IntentionPriority.High)
        {
            Output.WriteDebug($"[{args.Source}] {args.Message}");
        }
        else
        {
            string sourceIcon = args.Source switch
            {
                "user_persona" => "👤",
                "auto_training" => "🤖",
                _ => "🐍"
            };
            var displayMessage = args.Message.StartsWith("👤") || args.Message.StartsWith("🐍")
                ? args.Message
                : $"{sourceIcon} [{args.Source}] {args.Message}";
            Output.WriteSystem(displayMessage);
        }

        // Speak on voice side channel - await completion
        // Use distinct persona for user_persona to get a different voice
        if (args.Priority >= IntentionPriority.Normal)
        {
            try
            {
                var voicePersona = args.Source == "user_persona" ? "User" : null;
                await SayAndWaitAsyncFunc(args.Message, voicePersona);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Autonomy] Voice error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles intentions requiring user attention.
    /// </summary>
    internal void HandleIntentionAttention(Intention intention)
    {
        if (IsInConversationLoop()) return;

        var shortId = intention.Id.ToString()[..4];
        Output.WriteSystem($"⚡ {intention.Title} ({intention.Category}/{intention.Priority}) — /approve {shortId} | /reject {shortId}");

        // Announce intention on voice side channel
        if (intention.Priority >= IntentionPriority.Normal)
        {
            AnnounceAction($"Intention: {intention.Title}. {intention.Rationale}");
        }
    }

    /// <summary>
    /// Background loop that displays pending intentions and handles user interaction.
    /// </summary>
    internal async Task PushModeLoopAsync(CancellationToken ct)
    {
        // The PushModeLoop is now simpler since the AutonomousCoordinator handles
        // the tick loop internally. We just wait for events and keep the task alive.
        while (!ct.IsCancellationRequested && Coordinator != null)
        {
            try
            {
                // The coordinator handles its own tick loop and fires events
                // We just keep this task alive to monitor and potentially inject goals
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (InvalidOperationException ex)
            {
                Output.WriteWarning($"[push] {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUSH MODE COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Approves one or more pending intentions.
    /// </summary>
    internal async Task<string> ApproveIntentionAsync(string arg)
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = Coordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Approve all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to approve.";
            }

            foreach (var intention in pending)
            {
                var result = bus.ApproveIntentionByPartialId(intention.Id.ToString()[..8], "User approved all");
                sb.AppendLine(result
                    ? $"✓ Approved: [{intention.Id.ToString()[..8]}] {intention.Title}"
                    : $"✗ Failed to approve: {intention.Id}");
            }
        }
        else
        {
            // Approve specific intention by ID prefix
            var result = bus.ApproveIntentionByPartialId(arg, "User approved");
            sb.AppendLine(result
                ? $"✓ Approved intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Rejects one or more pending intentions.
    /// </summary>
    internal async Task<string> RejectIntentionAsync(string arg)
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = Coordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Reject all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to reject.";
            }

            foreach (var intention in pending)
            {
                bus.RejectIntentionByPartialId(intention.Id.ToString()[..8], "User rejected all");
                sb.AppendLine($"✗ Rejected: [{intention.Id.ToString()[..8]}] {intention.Title}");
            }
        }
        else
        {
            // Reject specific intention by ID prefix
            var result = bus.RejectIntentionByPartialId(arg, "User rejected");
            sb.AppendLine(result
                ? $"✗ Rejected intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Lists all pending intentions.
    /// </summary>
    internal string ListPendingIntentions()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var pending = Coordinator.IntentionBus.GetPendingIntentions().ToList();

        if (pending.Count == 0)
        {
            return "No pending intentions. Ouroboros will propose actions based on context.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   PENDING INTENTIONS                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        foreach (var intention in pending.OrderByDescending(i => i.Priority))
        {
            var priorityMarker = intention.Priority switch
            {
                IntentionPriority.Critical => "🔴",
                IntentionPriority.High => "🟠",
                IntentionPriority.Normal => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"  {priorityMarker} [{intention.Id.ToString()[..8]}] {intention.Category}");
            sb.AppendLine($"     {intention.Title}");
            sb.AppendLine($"     {intention.Description}");
            sb.AppendLine($"     Created: {intention.CreatedAt:HH:mm:ss}");
            sb.AppendLine();
        }

        sb.AppendLine("Commands: /approve <id|all> | /reject <id|all>");

        return sb.ToString();
    }

    /// <summary>
    /// Pauses push mode (stops proposing actions).
    /// </summary>
    internal string PausePushMode()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled.";
        }

        PushModeCts?.Cancel();
        return "⏸ Push mode paused. Use /resume to continue receiving proposals.";
    }

    /// <summary>
    /// Resumes push mode (continues proposing actions).
    /// </summary>
    internal string ResumePushMode()
    {
        if (Coordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        if (PushModeCts == null || PushModeCts.IsCancellationRequested)
        {
            PushModeCts?.Dispose();
            PushModeCts = new CancellationTokenSource();
            PushModeTask = Task.Run(() => PushModeLoopAsync(PushModeCts.Token), PushModeCts.Token);
            return "▶ Push mode resumed. Ouroboros will propose actions.";
        }

        return "Push mode is already active.";
    }
}
