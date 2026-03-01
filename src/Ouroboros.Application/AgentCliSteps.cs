// <copyright file="AgentCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.Agent;

namespace Ouroboros.Application;

/// <summary>
/// Autonomous agent CLI steps - behaves like GitHub Copilot.
/// The agent can plan, use tools, execute actions, and iterate until task completion.
/// Delegates tool management, prompt building, and action parsing to dedicated classes
/// under <see cref="Ouroboros.Application.Agent"/>.
/// </summary>
public static class AgentCliSteps
{
    private const int MaxConsecutiveThinks = 3;
    private const int ForceCompleteThinks = 5;
    private const int MaxConsecutiveUnknowns = 3;
    private const int MaxHistoryEntries = 20;
    private const int MaxExecutedActions = 30;

    /// <summary>
    /// Autonomous agent that plans and executes multi-step tasks.
    /// Usage: AutoAgent('Fix the bug in UserService.cs')
    /// Usage: AutoAgent('Add logging to all controllers;maxIter=10')
    /// </summary>
    [PipelineToken("AutoAgent", "Agent", "CopilotAgent")]
    public static Step<CliPipelineState, CliPipelineState> AutoAgent(string? args = null)
        => async s =>
        {
            var config = AutoAgentConfig.Parse(args);
            string task = config.Task ?? s.Query;

            if (string.IsNullOrWhiteSpace(task))
            {
                Console.WriteLine("[agent] No task provided");
                return s;
            }

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[AutoAgent] Task: {task}");
            Console.WriteLine($"[AutoAgent] Max iterations: {config.MaxIterations}");
            Console.WriteLine($"{'=',-60}\n");

            // Build agent tools and their description text
            var agentTools = AgentToolFactory.Build(s);
            var toolDescriptions = AgentPromptBuilder.BuildToolDescriptions();

            var conversationHistory = new List<AgentMessage>();
            var executedActions = new List<string>();
            bool taskComplete = false;

            int consecutiveThinks = 0;
            int consecutiveUnknowns = 0;
            int actionIterations = 0; // Only count non-think iterations toward the limit

            // Cap total iterations (including thinks/unknowns) to prevent unbounded looping
            int maxTotalIterations = config.MaxIterations * 3;
            for (int iteration = 1; actionIterations < config.MaxIterations && iteration <= maxTotalIterations && !taskComplete; iteration++)
            {
                Console.WriteLine($"\n[AutoAgent] === Iteration {iteration} (actions: {actionIterations}/{config.MaxIterations}) ===");

                // Build the agent prompt
                var agentPrompt = AgentPromptBuilder.Build(task, toolDescriptions, conversationHistory, executedActions);

                if (s.Trace) Console.WriteLine($"[AutoAgent] Prompt length: {agentPrompt.Length} chars");

                // Get agent's next action
                string agentResponse;
                try
                {
                    Console.WriteLine("[AutoAgent] Calling LLM...");
                    agentResponse = await s.Llm.InnerModel.GenerateTextAsync(agentPrompt);
                    Console.WriteLine($"[AutoAgent] LLM responded: {agentResponse.Length} chars");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Response: {StringHelpers.TruncateForDisplay(agentResponse, 300)}");
                }
                catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex)
                {
                    Console.WriteLine($"[AutoAgent] LLM error: {ex.Message}");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Stack: {ex.StackTrace}");
                    break;
                }

                conversationHistory.Add(new AgentMessage("assistant", agentResponse));

                // Parse the agent's response
                var action = AgentActionParser.Parse(agentResponse);

                if (action.Type == AgentActionType.Complete)
                {
                    Console.WriteLine($"\n[AutoAgent] ✓ Task completed!");
                    Console.WriteLine($"[AutoAgent] Summary: {action.Summary}");
                    taskComplete = true;
                    s.Output = action.Summary ?? "Task completed successfully.";
                }
                else if (action.Type == AgentActionType.UseTool && !string.IsNullOrEmpty(action.ToolName))
                {
                    consecutiveThinks = 0;
                    consecutiveUnknowns = 0;
                    actionIterations++;

                    Console.WriteLine($"[AutoAgent] Using tool: {action.ToolName}");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Args: {action.ToolArgs}");

                    // Execute the tool
                    var toolResult = await AgentToolExecutor.ExecuteAsync(
                        agentTools, action.ToolName, action.ToolArgs ?? string.Empty, s);

                    executedActions.Add(
                        $"[{action.ToolName}] {StringHelpers.TruncateForDisplay(action.ToolArgs, 50)} -> {StringHelpers.TruncateForDisplay(toolResult, 100)}");

                    // Add tool result to conversation
                    conversationHistory.Add(new AgentMessage("tool", $"[{action.ToolName}]: {toolResult}"));

                    Console.WriteLine($"[AutoAgent] Result: {StringHelpers.TruncateForDisplay(toolResult, 200)}");
                }
                else if (action.Type == AgentActionType.Think)
                {
                    consecutiveThinks++;
                    consecutiveUnknowns = 0;

                    Console.WriteLine($"[AutoAgent] Thinking: {action.Thought}");
                    conversationHistory.Add(new AgentMessage("system", $"Thought: {action.Thought}"));

                    if (consecutiveThinks >= ForceCompleteThinks)
                    {
                        Console.WriteLine("[AutoAgent] Forced completion after too many consecutive thoughts.");
                        s.Output = "Agent forced completion after excessive thinking without action.";
                        break;
                    }

                    if (consecutiveThinks >= MaxConsecutiveThinks)
                    {
                        conversationHistory.Add(new AgentMessage("system",
                            "You have been thinking for several iterations without taking action. " +
                            "Please use a tool or mark the task as complete. If you're stuck, use ask_user."));
                    }
                }
                else
                {
                    consecutiveUnknowns++;
                    consecutiveThinks = 0;

                    if (consecutiveUnknowns >= MaxConsecutiveUnknowns)
                    {
                        Console.WriteLine("[AutoAgent] Too many unparseable responses. Task aborted.");
                        s.Output = "Agent aborted: too many unparseable responses.";
                        break;
                    }

                    Console.WriteLine($"[AutoAgent] Unknown action, asking for clarification...");
                    conversationHistory.Add(new AgentMessage("system",
                        "Your response could not be parsed. Respond with valid JSON: " +
                        "either a tool call or a completion signal. Examples:\n" +
                        "{\"tool\": \"read_file\", \"args\": {\"path\": \"file.cs\"}}\n" +
                        "{\"complete\": true, \"summary\": \"Done\"}"));
                }

                // Cap conversation history to prevent unbounded memory growth
                if (conversationHistory.Count > MaxHistoryEntries)
                    conversationHistory.RemoveRange(0, conversationHistory.Count - MaxHistoryEntries);
                if (executedActions.Count > MaxExecutedActions)
                    executedActions.RemoveRange(0, executedActions.Count - MaxExecutedActions);
            }

            if (!taskComplete && string.IsNullOrEmpty(s.Output))
            {
                Console.WriteLine($"\n[AutoAgent] Max iterations reached ({config.MaxIterations})");
                s.Output = $"Task incomplete after {config.MaxIterations} iterations. Actions taken: {executedActions.Count}";
            }

            // Build final summary
            s.Context = string.Join("\n", executedActions);
            s.Query = task;

            return s;
        };
}
