using LangChain.DocumentLoaders;
using MediatR;
using Ouroboros.Application;
using Ouroboros.Application.Agent;
using Ouroboros.CLI.Commands;
using AgentMessage = Ouroboros.Application.Agent.AgentMessage;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunAutoAgentRequest"/>.
/// Wraps the autonomous agent pipeline step — plan, tool use, iterate — into a
/// handler that can be dispatched through the mediator bus.
///
/// Consolidates the Agent submodules (<see cref="AgentToolFactory"/>,
/// <see cref="AgentPromptBuilder"/>, <see cref="AgentToolExecutor"/>,
/// <see cref="AgentActionParser"/>) into a single handler entry point.
/// The static <c>AgentCliSteps.AutoAgent</c> is preserved for backward compatibility
/// but new callers should use this handler via MediatR.
/// </summary>
public sealed class RunAutoAgentHandler : IRequestHandler<RunAutoAgentRequest, string>
{
    private const int MaxConsecutiveThinks = 3;
    private const int ForceCompleteThinks = 5;
    private const int MaxConsecutiveUnknowns = 3;
    private const int MaxHistoryEntries = 20;
    private const int MaxExecutedActions = 30;

    private readonly OuroborosAgent _agent;

    public RunAutoAgentHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(RunAutoAgentRequest request, CancellationToken cancellationToken)
    {
        var llm = _agent.ModelsSub.Llm;
        var embedding = _agent.ModelsSub.Embedding;
        var tools = _agent.ToolsSub.Tools;

        if (llm == null)
            return "Agent LLM not available. Ensure the agent is fully initialized.";

        // Build a CliPipelineState for the agent tools — same pattern as ProcessDslHandler
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"auto-agent-{Guid.NewGuid().ToString()[..8]}", store, dataSource);

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embedding,
            Query = request.Task,
            Trace = _agent.Config.Debug,
            CancellationToken = cancellationToken,
        };

        // Build agent tools and their description text
        var agentTools = AgentToolFactory.Build(state);
        var toolDescriptions = AgentPromptBuilder.BuildToolDescriptions();

        var conversationHistory = new List<AgentMessage>();
        var executedActions = new List<string>();
        var resultBuilder = new System.Text.StringBuilder();

        int consecutiveThinks = 0;
        int consecutiveUnknowns = 0;
        int actionIterations = 0; // Only count non-think iterations toward the limit

        for (int iteration = 1; actionIterations < request.MaxIterations; iteration++)
        {
            // Bug 2 fix: propagate cancellation token
            cancellationToken.ThrowIfCancellationRequested();

            var agentPrompt = AgentPromptBuilder.Build(
                request.Task, toolDescriptions, conversationHistory, executedActions);

            string agentResponse;
            try
            {
                agentResponse = await llm.InnerModel.GenerateTextAsync(agentPrompt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                resultBuilder.AppendLine("[AutoAgent] Cancelled by user.");
                break;
            }
            catch (Exception ex)
            {
                resultBuilder.AppendLine($"[AutoAgent] LLM error at iteration {iteration}: {ex.Message}");
                break;
            }

            conversationHistory.Add(new AgentMessage("assistant", agentResponse));

            var action = AgentActionParser.Parse(agentResponse);

            if (action.Type == AgentActionType.Complete)
            {
                resultBuilder.AppendLine(action.Summary ?? "Task completed.");
                break;
            }

            if (action.Type == AgentActionType.UseTool && !string.IsNullOrEmpty(action.ToolName))
            {
                consecutiveThinks = 0;
                consecutiveUnknowns = 0;
                actionIterations++;

                var toolResult = await AgentToolExecutor.ExecuteAsync(
                    agentTools, action.ToolName, action.ToolArgs ?? "", state);
                var display = (toolResult ?? "").Replace("\r\n", " ").Replace("\n", " ");
                if (display.Length > 200) display = display[..200] + "...";
                executedActions.Add($"[{action.ToolName}] {display}");
                conversationHistory.Add(new AgentMessage("tool", toolResult));
            }
            else if (action.Type == AgentActionType.Think)
            {
                // Bug 1 fix: track consecutive thinks, don't count toward iteration limit
                consecutiveThinks++;
                consecutiveUnknowns = 0;

                conversationHistory.Add(new AgentMessage("system", $"Thought: {action.Thought}"));

                if (consecutiveThinks >= ForceCompleteThinks)
                {
                    resultBuilder.AppendLine("[AutoAgent] Forced completion after too many consecutive thoughts.");
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
                // Bug 3 fix: track unknown actions, abort after too many
                consecutiveUnknowns++;
                consecutiveThinks = 0;

                if (consecutiveUnknowns >= MaxConsecutiveUnknowns)
                {
                    resultBuilder.AppendLine("[AutoAgent] Too many unparseable responses. Task aborted.");
                    break;
                }

                conversationHistory.Add(new AgentMessage("system",
                    "Your response could not be parsed. Respond with valid JSON: " +
                    "either a tool call or a completion signal. Examples:\n" +
                    "{\"tool\": \"read_file\", \"args\": {\"path\": \"file.cs\"}}\n" +
                    "{\"complete\": true, \"summary\": \"Done\"}"));
            }

            // Bug 4 fix: cap conversation history to prevent unbounded memory growth
            if (conversationHistory.Count > MaxHistoryEntries)
                conversationHistory.RemoveRange(0, conversationHistory.Count - MaxHistoryEntries);
            if (executedActions.Count > MaxExecutedActions)
                executedActions.RemoveRange(0, executedActions.Count - MaxExecutedActions);
        }

        return resultBuilder.Length > 0
            ? resultBuilder.ToString().Trim()
            : "Agent completed without explicit result.";
    }
}
