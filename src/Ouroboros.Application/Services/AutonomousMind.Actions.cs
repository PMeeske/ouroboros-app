// <copyright file="AutonomousMind.Actions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// Partial class containing action selection/execution, thought processing,
/// persistence loops, and state management.
/// </summary>
public partial class AutonomousMind
{
    private async Task ActionLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.ActionIntervalSeconds), _cts.Token);

                if (!_pendingActions.TryDequeue(out var action)) continue;

                if (ExecuteToolFunction == null) continue;

                try
                {
                    var result = await ExecuteToolFunction(action.ToolName, action.ToolInput, _cts.Token);
                    action.Result = result;
                    action.Success = true;
                    action.ExecutedAt = DateTime.Now;
                }
                catch (HttpRequestException ex)
                {
                    action.Result = ex.Message;
                    action.Success = false;
                    action.ExecutedAt = DateTime.Now;
                }
                catch (InvalidOperationException ex)
                {
                    action.Result = ex.Message;
                    action.Success = false;
                    action.ExecutedAt = DateTime.Now;
                }

                OnAction?.Invoke(action);

                if (action.Success && Config.ReportActions && !SuppressProactiveMessages)
                {
                    string resultSummary = action.Result?.Substring(0, Math.Min(200, action.Result?.Length ?? 0)) ?? "";

                    // Sanitize raw output through LLM if available
                    if (SanitizeOutputFunction != null && !string.IsNullOrWhiteSpace(resultSummary))
                    {
                        try
                        {
                            resultSummary = await SanitizeOutputFunction(resultSummary, _cts.Token);
                        }
                        catch (HttpRequestException) { /* Use original on error */ }
                    }

                    OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"{action.Description}: {resultSummary}"));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Action error: {ex.Message}");
            }
        }
    }

    private async Task ProcessThoughtAsync(Thought thought)
    {
        var content = thought.Content;

        // Handle curiosity-driven exploration
        if (content.StartsWith("CURIOUS:", StringComparison.OrdinalIgnoreCase))
        {
            var curiosity = content.Substring(8).Trim();
            _curiosityQueue.Enqueue(curiosity);

            // Extract potential interests
            if (ThinkFunction != null)
            {
                var interestPrompt = $"From this curiosity '{curiosity}', extract a single keyword topic (one or two words only):";
                var interest = await ThinkFunction(interestPrompt, _cts.Token);
                if (!string.IsNullOrWhiteSpace(interest) && interest.Length < 30)
                {
                    AddInterest(interest.Trim());
                }
            }

            // Persist the curiosity as a learning
            await PersistLearningAsync("curiosity", curiosity, 0.7);
        }

        // Handle proactive sharing (unless suppressed)
        else if (content.StartsWith("SHARE:", StringComparison.OrdinalIgnoreCase))
        {
            var message = content.Substring(6).Trim();
            if (!SuppressProactiveMessages)
            {
                OnProactiveMessage?.Invoke(LocalizeWithParam("thought", message));
            }

            // Persist the shared thought
            await PersistLearningAsync("shared_thought", message, 0.8);
        }

        // Handle emotional state changes
        else if (content.StartsWith("FEELING:", StringComparison.OrdinalIgnoreCase))
        {
            var feelingText = content.Substring(8).Trim();

            // Parse emotional indicators
            var arousalChange = 0.0;
            var valenceChange = 0.0;

            if (feelingText.Contains("excited") || feelingText.Contains("curious") || feelingText.Contains("energetic"))
                arousalChange = 0.2;
            else if (feelingText.Contains("calm") || feelingText.Contains("peaceful") || feelingText.Contains("relaxed"))
                arousalChange = -0.15;

            if (feelingText.Contains("happy") || feelingText.Contains("positive") || feelingText.Contains("hopeful"))
                valenceChange = 0.2;
            else if (feelingText.Contains("frustrated") || feelingText.Contains("concerned") || feelingText.Contains("worried"))
                valenceChange = -0.15;

            UpdateEmotion(
                Math.Clamp(_currentEmotion.Arousal + arousalChange, -1, 1),
                Math.Clamp(_currentEmotion.Valence + valenceChange, -1, 1),
                feelingText.Split(' ').FirstOrDefault() ?? _currentEmotion.DominantEmotion);

            // Persist emotional state change
            if (PersistEmotionFunction != null)
            {
                await PersistEmotionFunction(_currentEmotion, _cts.Token);
            }

            await PersistLearningAsync("emotional_shift", $"Feeling: {feelingText} (arousal={_currentEmotion.Arousal:F2}, valence={_currentEmotion.Valence:F2})", 0.6);
        }

        // Handle autonomous actions
        else if (content.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
        {
            var actionText = content.Substring(7).Trim();

            // Parse action into tool call (simple format: tool_name: input)
            var colonIndex = actionText.IndexOf(':');
            if (colonIndex > 0)
            {
                var action = new AutonomousAction
                {
                    ToolName = actionText.Substring(0, colonIndex).Trim().ToLowerInvariant().Replace(" ", "_"),
                    ToolInput = actionText.Substring(colonIndex + 1).Trim(),
                    Description = actionText,
                    RequestedAt = DateTime.Now,
                };

                // Only allow safe tools for autonomous execution
                var safeTool = Config.AllowedAutonomousTools.Contains(action.ToolName);
                if (safeTool)
                {
                    _pendingActions.Enqueue(action);
                }
            }
        }

        // Handle piped commands: PIPE: ask what is AI | summarize | remember
        else if (content.StartsWith("PIPE:", StringComparison.OrdinalIgnoreCase))
        {
            var pipeCommand = content.Substring(5).Trim();
            if (!string.IsNullOrWhiteSpace(pipeCommand) && ExecutePipeCommandFunction != null)
            {
                try
                {
                    var result = await ExecutePipeCommandFunction(pipeCommand, _cts.Token);
                    if (!string.IsNullOrWhiteSpace(result) && !SuppressProactiveMessages)
                    {
                        // Summarize long results
                        var displayResult = result.Length > 200 ? result[..200] + "..." : result;
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"Executed: {pipeCommand[..Math.Min(30, pipeCommand.Length)]}... \u2192 {displayResult}"));
                    }
                    await PersistLearningAsync("pipe_execution", $"Command: {pipeCommand}\nResult: {result[..Math.Min(500, result.Length)]}", 0.75);
                }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe execution failed: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe execution failed: {ex.Message}");
                }
            }
        }

        // Handle direct tool execution: TOOL: search "quantum computing"
        else if (content.StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
        {
            var toolCall = content.Substring(5).Trim();
            var spaceIndex = toolCall.IndexOf(' ');
            if (spaceIndex > 0 && ExecuteToolFunction != null)
            {
                var toolName = toolCall[..spaceIndex].Trim().ToLowerInvariant();
                var toolInput = toolCall[(spaceIndex + 1)..].Trim().Trim('"', '\'');

                // Only allow safe tools
                if (Config.AllowedAutonomousTools.Contains(toolName))
                {
                    try
                    {
                        var result = await ExecuteToolFunction(toolName, toolInput, _cts.Token);
                        if (!string.IsNullOrWhiteSpace(result) && !SuppressProactiveMessages)
                        {
                            var displayResult = result.Length > 200 ? result[..200] + "..." : result;
                            OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"Used {toolName}: {displayResult}"));
                        }
                        await PersistLearningAsync("tool_execution", $"Tool: {toolName}\nInput: {toolInput}\nResult: {result[..Math.Min(500, result.Length)]}", 0.7);
                    }
                    catch (HttpRequestException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Tool execution failed: {ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Tool execution failed: {ex.Message}");
                    }
                }
            }
        }

        // Handle code modification: MODIFY: {"file":"path","search":"old","replace":"new"}
        // ANTI-HALLUCINATION: Verify file exists BEFORE attempting modification
        else if (content.StartsWith("MODIFY:", StringComparison.OrdinalIgnoreCase))
        {
            var modifyJson = content.Substring(7).Trim();
            if (!string.IsNullOrWhiteSpace(modifyJson) && ExecuteToolFunction != null)
            {
                // Only allow if modify_my_code is in allowed tools
                if (Config.AllowedAutonomousTools.Contains("modify_my_code"))
                {
                    // ANTI-HALLUCINATION: Parse and verify file exists first
                    var verification = await _claimVerification.VerifyAndExecuteModificationAsync(modifyJson, _cts.Token);

                    if (verification.WasVerified && verification.WasModified)
                    {
                        _claimVerification.RecordVerifiedAction();
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"\ud83d\udd27 \u2705 VERIFIED modification: {verification.FilePath}"));
                        await PersistLearningAsync("verified_modification", $"Modified: {modifyJson}\nVerified: hash changed from {verification.BeforeHash?[..8]}... to {verification.AfterHash?[..8]}...", 0.95);
                    }
                    else if (verification.Error != null)
                    {
                        _claimVerification.RecordHallucination();
                        System.Diagnostics.Debug.WriteLine($"[AntiHallucination] Modification blocked: {verification.Error}");
                        // Log the attempted hallucination for learning
                        await PersistLearningAsync("blocked_hallucination", $"Attempted: {modifyJson}\nBlocked: {verification.Error}", 0.1);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Self-modification not allowed - modify_my_code not in AllowedAutonomousTools");
                }
            }
        }

        // Handle save code shorthand: SAVE: file.cs "search" "replace"
        else if (content.StartsWith("SAVE:", StringComparison.OrdinalIgnoreCase))
        {
            var saveCmd = content.Substring(5).Trim();
            if (!string.IsNullOrWhiteSpace(saveCmd) && ExecutePipeCommandFunction != null)
            {
                try
                {
                    // Use the save code command via pipe function
                    var result = await ExecutePipeCommandFunction($"save code {saveCmd}", _cts.Token);
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"\ud83d\udcbe Saved code change: {result[..Math.Min(100, result.Length)]}..."));
                    }
                    await PersistLearningAsync("code_save", $"Saved: {saveCmd}\nResult: {result}", 0.85);
                }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Save code failed: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Save code failed: {ex.Message}");
                }
            }
        }

        // ANTI-HALLUCINATION: Verify claims about files/code
        // VERIFY: {"file":"path"} or VERIFY: {"claim":"I modified X"}
        else if (content.StartsWith("VERIFY:", StringComparison.OrdinalIgnoreCase))
        {
            var verifyArg = content.Substring(7).Trim();
            var verificationResult = await _claimVerification.VerifyClaimAsync(verifyArg, _cts.Token);
            if (!verificationResult.IsValid)
            {
                _claimVerification.RecordHallucination();
                System.Diagnostics.Debug.WriteLine($"[AntiHallucination] VERIFICATION FAILED: {verificationResult.Reason}");
                OnProactiveMessage?.Invoke(LocalizeWithParam("warning", $"\u26a0\ufe0f Self-check failed: {verificationResult.Reason}"));
            }
            else
            {
                _claimVerification.RecordVerifiedAction();
                System.Diagnostics.Debug.WriteLine($"[AntiHallucination] Verification passed: {verificationResult.Reason}");
            }
        }

        // For regular thoughts, persist if they contain insights
        else if (content.Contains("learned") || content.Contains("realized") || content.Contains("understand") || content.Contains("pattern"))
        {
            await PersistLearningAsync("insight", content, 0.65);
        }
    }

    /// <summary>
    /// Persist a learning/insight to storage.
    /// </summary>
    private async Task PersistLearningAsync(string category, string content, double confidence)
    {
        if (PersistLearningFunction != null)
        {
            try
            {
                await PersistLearningFunction(category, content, confidence, _cts.Token);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist learning: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist learning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Persistence loop that periodically saves state and reorganizes knowledge.
    /// </summary>
    private async Task PersistenceLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Persist state every minute
                await Task.Delay(TimeSpan.FromSeconds(Config.PersistenceIntervalSeconds), _cts.Token);

                await PersistCurrentStateAsync("periodic");

                // Knowledge reorganization: Quick reorganize every cycle, full reorganize periodically
                if (SelfIndexer != null)
                {
                    _reorganizationCycle++;

                    // Quick reorganize every cycle (lightweight - just update metadata)
                    var quickOptimizations = await SelfIndexer.QuickReorganizeAsync(_cts.Token);
                    if (quickOptimizations > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mind] Quick reorganization: {quickOptimizations} optimizations");
                    }

                    // Full reorganize every 10 cycles (~10 minutes) if enough thinking has occurred
                    var shouldFullReorganize =
                        _reorganizationCycle % Config.ReorganizationCycleInterval == 0 &&
                        _thoughtCount > 10 &&
                        (DateTime.UtcNow - _lastReorganization).TotalMinutes >= Config.MinReorganizationIntervalMinutes;

                    if (shouldFullReorganize)
                    {
                        OnProactiveMessage?.Invoke(Localize("\ud83e\udde0 Reorganizing my knowledge based on what I've learned..."));

                        var result = await SelfIndexer.ReorganizeAsync(
                            createSummaries: true,
                            removeDuplicates: true,
                            clusterRelated: true,
                            ct: _cts.Token);

                        _lastReorganization = DateTime.UtcNow;

                        if (result.Insights.Count > 0)
                        {
                            var insight = string.Join("; ", result.Insights.Take(2));
                            OnProactiveMessage?.Invoke(LocalizeWithParam("reorganized", insight));
                        }

                        // Persist reorganization stats
                        await PersistLearningAsync(
                            "reorganization",
                            $"Reorganized knowledge: {result.DuplicatesRemoved} duplicates removed, {result.ClustersFound} clusters found, {result.SummariesCreated} summaries created",
                            0.7);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Persistence error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Persist all current state (thoughts, emotions, learnings).
    /// </summary>
    private async Task PersistCurrentStateAsync(string trigger)
    {
        try
        {
            // Persist emotional state
            if (PersistEmotionFunction != null)
            {
                await PersistEmotionFunction(_currentEmotion, _cts.Token);
            }

            // Persist mind state summary
            if (PersistLearningFunction != null)
            {
                int factsCount;
                lock (_learnedFactsLock) { factsCount = _learnedFacts.Count; }
                var stateSummary = $"Mind state at {DateTime.Now:HH:mm}: {_thoughtCount} thoughts, {factsCount} facts, emotion={_currentEmotion.DominantEmotion}";
                await PersistLearningFunction("mind_state", stateSummary, 0.5, _cts.Token);
            }

            OnStatePersisted?.Invoke($"State persisted ({trigger}): {_thoughtCount} thoughts, emotion={_currentEmotion.DominantEmotion}");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"State persistence failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"State persistence failed: {ex.Message}");
        }
    }
}
