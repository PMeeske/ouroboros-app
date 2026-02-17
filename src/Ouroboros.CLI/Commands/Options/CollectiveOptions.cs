using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Collective mind / multi-provider orchestration.
/// Shared by: ask, orchestrator.
/// </summary>
public sealed class CollectiveOptions : IComposableOptions
{
    public Option<string> CollectiveOption { get; } = new("--collective")
    {
        Description = "Enable CollectiveMind multi-provider mode",
        DefaultValueFactory = _ => "off"
    };

    public Option<string?> MasterModelOption { get; } = new("--master-model")
    {
        Description = "Designate master model for orchestration"
    };

    public Option<string> ElectionStrategyOption { get; } = new("--election-strategy")
    {
        Description = "Election strategy: majority|weighted|borda|condorcet|runoff|approval|master",
        DefaultValueFactory = _ => "weighted"
    };

    public Option<bool> ShowSubgoalsOption { get; } = new("--show-subgoals")
    {
        Description = "Display sub-goal decomposition trace",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ParallelSubgoalsOption { get; } = new("--parallel-subgoals")
    {
        Description = "Execute independent sub-goals in parallel",
        DefaultValueFactory = _ => true
    };

    public Option<string> DecomposeOption { get; } = new("--decompose")
    {
        Description = "Enable goal decomposition mode: off|auto|local-first|quality-first",
        DefaultValueFactory = _ => "off"
    };

    public void AddToCommand(Command command)
    {
        command.Add(CollectiveOption);
        command.Add(MasterModelOption);
        command.Add(ElectionStrategyOption);
        command.Add(ShowSubgoalsOption);
        command.Add(ParallelSubgoalsOption);
        command.Add(DecomposeOption);
    }
}