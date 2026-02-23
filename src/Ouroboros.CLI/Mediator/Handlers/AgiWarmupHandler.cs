using MediatR;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AgiWarmupRequest"/>.
/// Performs AGI warmup at startup — primes the model with examples for autonomous operation.
/// </summary>
public sealed class AgiWarmupHandler : IRequestHandler<AgiWarmupRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public AgiWarmupHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(AgiWarmupRequest request, CancellationToken ct)
    {
        try
        {
            if (_agent.Config.Verbosity != OutputVerbosity.Quiet)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n  \u23f3 Warming up AGI systems...");
                Console.ResetColor();
            }

            var autonomousMind = _agent.AutonomySub.AutonomousMind;
            var selfIndexer = _agent.AutonomySub.SelfIndexer;
            var tools = _agent.ToolsSub.Tools;

            var agiWarmup = new AgiWarmup(
                thinkFunction: autonomousMind?.ThinkFunction,
                searchFunction: autonomousMind?.SearchFunction,
                executeToolFunction: autonomousMind?.ExecuteToolFunction,
                selfIndexer: selfIndexer,
                toolRegistry: tools);

            _agent.EmbodimentSub.AgiWarmup = agiWarmup;

            if (autonomousMind != null)
            {
                autonomousMind.Config.ThinkingIntervalSeconds = 15;
            }

            if (_agent.Config.Verbosity == OutputVerbosity.Verbose)
            {
                agiWarmup.OnProgress += (step, percent) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\r  \u23f3 {step} ({percent}%)".PadRight(60));
                    Console.ResetColor();
                };
            }

            var result = await agiWarmup.WarmupAsync();

            if (_agent.Config.Verbosity == OutputVerbosity.Verbose)
            {
                Console.WriteLine(); // Clear progress line

                if (result.Success)
                    _agent.ConsoleOutput.WriteDebug($"AGI warmup complete in {result.Duration.TotalSeconds:F1}s");
                else
                    _agent.ConsoleOutput.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }
            else if (_agent.Config.Verbosity != OutputVerbosity.Quiet)
            {
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  \u2713 Autonomous mind active");
                    Console.ResetColor();
                }
                else
                    _agent.ConsoleOutput.WriteWarning($"AGI warmup limited: {result.Error ?? "Some features unavailable"}");
            }

            // Warmup thought seeded into curiosity queue rather than displayed (shifts with conversation)
            if (result.Success && !string.IsNullOrEmpty(result.WarmupThought))
            {
                autonomousMind?.InjectTopic(result.WarmupThought);
            }

            // Trigger Scrutor assembly scan now that all subsystems are registered —
            // discovers all ITool implementations and builds the IServiceProvider.
            _ = Ouroboros.Application.Tools.ServiceContainerFactory.Build();
        }
        catch (Exception ex)
        {
            _agent.ConsoleOutput.WriteWarning($"AGI warmup skipped: {ex.Message}");
        }

        return Unit.Value;
    }
}
