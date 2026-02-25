using LangChain.DocumentLoaders;
using MediatR;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessDslRequest"/>.
/// Extracted from <c>OuroborosAgent.ProcessDslAsync</c>.
/// Parses and executes a pipeline DSL string, tracking network state and capabilities.
/// </summary>
public sealed class ProcessDslHandler : IRequestHandler<ProcessDslRequest, string>
{
    private readonly OuroborosAgent _agent;

    public ProcessDslHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(ProcessDslRequest request, CancellationToken cancellationToken)
    {
        var dsl = request.Dsl;
        var config = _agent.Config;
        var output = _agent.ConsoleOutput;
        var llm = _agent.ModelsSub.Llm;
        var embedding = _agent.ModelsSub.Embedding;
        var tools = _agent.ToolsSub.Tools;
        var networkTracker = _agent.AutonomySub.NetworkTracker;
        var capabilityRegistry = _agent.AutonomySub.CapabilityRegistry;
        var globalWorkspace = _agent.AutonomySub.GlobalWorkspace;

        try
        {
            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\ud83d\udcdc Executing DSL: {Markup.Escape(dsl)}[/]\n");

            // Explain the DSL first
            var explanation = PipelineDsl.Explain(dsl);
            AnsiConsole.MarkupLine(Markup.Escape(explanation));

            // Build and execute the pipeline
            if (embedding != null && llm != null)
            {
                var store = new TrackedVectorStore();
                var dataSource = DataSource.FromPath(".");
                var branch = new PipelineBranch("ouroboros-dsl", store, dataSource);

                var state = new CliPipelineState
                {
                    Branch = branch,
                    Llm = llm,
                    Tools = tools,
                    Embed = embedding,
                    Trace = config.Debug,
                    NetworkTracker = networkTracker  // Enable automatic step reification
                };

                // Initial tracking of the branch
                networkTracker?.TrackBranch(branch);

                // Track capability usage for self-improvement
                var startTime = DateTime.UtcNow;
                var success = true;

                try
                {
                    var step = PipelineDsl.Build(dsl);
                    state = await step(state);
                }
                catch (Exception stepEx)
                {
                    success = false;
                    throw new InvalidOperationException($"Pipeline step failed: {stepEx.Message}", stepEx);
                }

                // Final update to capture all step events
                if (networkTracker != null)
                {
                    var trackResult = networkTracker.UpdateBranch(state.Branch);
                    if (config.Debug)
                    {
                        var stepEvents = state.Branch.Events.OfType<StepExecutionEvent>().ToList();
                        AnsiConsole.MarkupLine($"  \ud83d\udcca Network state: {Markup.Escape(trackResult.Value.ToString())} events reified ({stepEvents.Count} steps tracked)");
                        foreach (var stepEvt in stepEvents.TakeLast(5))
                        {
                            var status = stepEvt.Success ? "\u2713" : "\u2717";
                            AnsiConsole.MarkupLine($"      {Markup.Escape(status)} [[{Markup.Escape(stepEvt.TokenName)}]] {Markup.Escape(stepEvt.Description)} ({stepEvt.DurationMs}ms)");
                        }
                    }
                }

                // Track capability usage for self-improvement
                var duration = DateTime.UtcNow - startTime;
                if (capabilityRegistry != null)
                {
                    var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(success, duration, dsl);
                    await capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
                }

                // Update global workspace with execution result
                globalWorkspace?.AddItem(
                    $"DSL Executed: {dsl[..Math.Min(100, dsl.Length)]}\nDuration: {duration.TotalSeconds:F2}s",
                    WorkspacePriority.Normal,
                    "dsl-execution",
                    new List<string> { "dsl", "pipeline", success ? "success" : "failure" });

                AnsiConsole.MarkupLine(OuroborosTheme.Ok("\n  \u2713 Pipeline completed"));

                // Get last reasoning output
                var lastReasoning = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault();
                if (lastReasoning != null)
                {
                    AnsiConsole.MarkupLine($"\n{Markup.Escape(lastReasoning.State.Text)}");
                    await _agent.VoiceService.SayAsync(lastReasoning.State.Text);
                }
                else if (!string.IsNullOrEmpty(state.Output))
                {
                    AnsiConsole.MarkupLine($"\n{Markup.Escape(state.Output)}");
                    await _agent.VoiceService.SayAsync(state.Output);
                }
            }
            else
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn("  \u26a0 Cannot execute DSL: LLM or embeddings not available"));
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            // Track failure for self-improvement
            if (capabilityRegistry != null)
            {
                var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(false, TimeSpan.Zero, dsl);
                await capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
            }

            AnsiConsole.MarkupLine($"[red]{Markup.Escape($"DSL execution failed: {ex.Message}")}[/]");

            return string.Empty;
        }
    }
}
