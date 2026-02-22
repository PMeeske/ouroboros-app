using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the metta command. Maps to <see cref="Ouroboros.Options.MeTTaOptions"/>.
/// </summary>
public sealed class MeTTaCommandOptions
{
    // ── Composed option groups ─────────────────────────────────────────
    public ModelOptions Model { get; } = new();
    public EndpointOptions Endpoint { get; } = new();
    public DiagnosticOptions Diagnostics { get; } = new();
    public EmbeddingOptions Embedding { get; } = new();
    public CommandVoiceOptions Voice { get; } = new();

    // ── MeTTa-specific options ─────────────────────────────────────────

    public Option<string> GoalOption { get; } = new("--goal", "-g")
    {
        Description = "Goal or task for the MeTTa orchestrator to plan and execute",
        DefaultValueFactory = _ => string.Empty
    };

    public Option<string?> CultureOption { get; } = new("--culture", "-c")
    {
        Description = "Target culture for the response (e.g. en-US, fr-FR, es)"
    };

    public Option<bool> PlanOnlyOption { get; } = new("--plan-only")
    {
        Description = "Only generate plan without execution",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ShowMetricsOption { get; } = new("--metrics")
    {
        Description = "Display performance metrics",
        DefaultValueFactory = _ => true
    };

    public Option<bool> InteractiveOption { get; } = new("--interactive", "-i")
    {
        Description = "Enter interactive MeTTa REPL mode",
        DefaultValueFactory = _ => false
    };

    public Option<string> PersonaOption { get; } = new("--persona")
    {
        Description = "Persona name for voice mode",
        DefaultValueFactory = _ => "Iaret"
    };

    public void AddToCommand(Command command)
    {
        // MeTTa-specific
        command.Add(GoalOption);
        command.Add(CultureOption);
        command.Add(PlanOnlyOption);
        command.Add(ShowMetricsOption);
        command.Add(InteractiveOption);
        command.Add(PersonaOption);

        // Composed groups
        Model.AddToCommand(command);
        Endpoint.AddToCommand(command);
        Diagnostics.AddToCommand(command);
        Embedding.AddToCommand(command);
        Voice.AddToCommand(command);
    }
}
