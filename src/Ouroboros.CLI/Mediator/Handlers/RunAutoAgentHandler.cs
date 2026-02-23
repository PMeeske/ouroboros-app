using LangChain.DocumentLoaders;
using MediatR;
using Ouroboros.Application;
using Ouroboros.Application.Agent;
using Ouroboros.CLI.Commands;

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
        };

        // Build agent tools and their description text
        var agentTools = AgentToolFactory.Build(state);
        var toolDescriptions = AgentPromptBuilder.BuildToolDescriptions();

        var conversationHistory = new List<AgentMessage>();
        var executedActions = new List<string>();
        var resultBuilder = new System.Text.StringBuilder();

        for (int iteration = 1; iteration <= request.MaxIterations; iteration++)
        {
            var agentPrompt = AgentPromptBuilder.Build(
                request.Task, toolDescriptions, conversationHistory, executedActions);

            string agentResponse;
            try
            {
                agentResponse = await llm.InnerModel.GenerateTextAsync(agentPrompt);
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
                resultBuilder.AppendLine(action.Content ?? "Task completed.");
                break;
            }

            if (action.Type == AgentActionType.UseTool && !string.IsNullOrEmpty(action.ToolName))
            {
                var toolResult = await AgentToolExecutor.ExecuteAsync(
                    agentTools, action.ToolName, action.ToolArgs ?? "", state);
                executedActions.Add($"[{action.ToolName}] {StringHelpers.TruncateForDisplay(toolResult, 200)}");
                conversationHistory.Add(new AgentMessage("tool", toolResult));
            }
            else if (action.Type == AgentActionType.Think)
            {
                conversationHistory.Add(new AgentMessage("system", $"Thought: {action.Content}"));
            }
        }

        return resultBuilder.Length > 0
            ? resultBuilder.ToString().Trim()
            : "Agent completed without explicit result.";
    }
}
