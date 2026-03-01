using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the cognitive-physics command.
/// </summary>
public sealed class CognitivePhysicsCommandHandler
{
    private readonly ICognitivePhysicsService _cpeService;
    private readonly ISpectreConsoleService _console;
    private readonly ILogger<CognitivePhysicsCommandHandler> _logger;

    public CognitivePhysicsCommandHandler(
        ICognitivePhysicsService cpeService,
        ISpectreConsoleService console,
        ILogger<CognitivePhysicsCommandHandler> logger)
    {
        _cpeService = cpeService;
        _console = console;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        string operation,
        string focus,
        string? target,
        string[]? targets,
        double resources,
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (operation.ToLowerInvariant())
            {
                case "shift":
                    return await HandleShiftAsync(focus, target, resources, verbose);

                case "trajectory":
                    return await HandleTrajectoryAsync(focus, targets, resources, verbose);

                case "entangle":
                    return await HandleEntangleAsync(focus, targets, resources);

                case "chaos":
                    return HandleChaos(focus, resources, verbose);

                default:
                    _console.MarkupLine($"[red]Unknown operation:[/] {operation}. Use: shift, trajectory, entangle, chaos");
                    return 1;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error executing cognitive-physics command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<int> HandleShiftAsync(string focus, string? target, double resources, bool verbose)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _console.MarkupLine("[red]Error:[/] --target is required for shift operation.");
            return 1;
        }

        await _console.Status().StartAsync($"ZeroShift: {focus} → {target}...", async ctx =>
        {
            var result = await _cpeService.ShiftAsync(focus, target, resources);
            ctx.Status = "Done";

            if (result.IsSuccess)
            {
                var s = result.Value;
                _console.MarkupLine("[green]Shift succeeded[/]");
                _console.MarkupLine($"  Focus:       [cyan]{s.Focus}[/]");
                _console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
                _console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
                _console.MarkupLine($"  Cooldown:    [yellow]{s.Cooldown:F1}[/]");
                if (verbose)
                    _console.MarkupLine($"  History:     {string.Join(" → ", s.History)}");
            }
            else
            {
                _console.MarkupLine($"[red]Shift failed:[/] {result.Error}");
            }
        });

        return 0;
    }

    private async Task<int> HandleTrajectoryAsync(string focus, string[]? targets, double resources, bool verbose)
    {
        var targetList = targets?.ToList() ?? [];
        if (targetList.Count == 0)
        {
            _console.MarkupLine("[red]Error:[/] --targets is required for trajectory operation.");
            return 1;
        }

        await _console.Status().StartAsync($"Trajectory: {focus} → [{string.Join(" → ", targetList)}]...", async ctx =>
        {
            var result = await _cpeService.ExecuteTrajectoryAsync(focus, targetList, resources);
            ctx.Status = "Done";

            if (result.IsSuccess)
            {
                var s = result.Value;
                _console.MarkupLine("[green]Trajectory completed[/]");
                _console.MarkupLine($"  Final Focus: [cyan]{s.Focus}[/]");
                _console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
                _console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
                if (verbose)
                    _console.MarkupLine($"  Path:        {string.Join(" → ", s.History)}");
            }
            else
            {
                _console.MarkupLine($"[red]Trajectory failed:[/] {result.Error}");
            }
        });

        return 0;
    }

    private async Task<int> HandleEntangleAsync(string focus, string[]? targets, double resources)
    {
        var branchTargets = targets?.ToList() ?? [];
        if (branchTargets.Count == 0)
        {
            _console.MarkupLine("[red]Error:[/] --targets is required for entangle operation.");
            return 1;
        }

        await _console.Status().StartAsync($"Entangling: {focus} → [{string.Join(", ", branchTargets)}]...", async ctx =>
        {
            var branches = await _cpeService.EntangleAsync(focus, branchTargets, resources);
            ctx.Status = "Done";

            _console.MarkupLine($"[green]Entangled into {branches.Count} branches[/]");
            var table = new Table();
            table.AddColumn("Branch");
            table.AddColumn("Focus");
            table.AddColumn("Weight");
            table.AddColumn("Resources");

            for (int i = 0; i < branches.Count; i++)
            {
                var b = branches[i];
                table.AddRow(
                    $"#{i + 1}",
                    b.State.Focus,
                    $"{b.Weight:F3}",
                    $"{b.State.Resources:F1}");
            }

            _console.Write(table);
        });

        return 0;
    }

    private int HandleChaos(string focus, double resources, bool verbose)
    {
        var result = _cpeService.InjectChaos(focus, resources);

        if (result.IsSuccess)
        {
            var s = result.Value;
            _console.MarkupLine("[green]Chaos injected[/]");
            _console.MarkupLine($"  Focus:       [cyan]{s.Focus}[/]");
            _console.MarkupLine($"  Resources:   [yellow]{s.Resources:F1}[/]");
            _console.MarkupLine($"  Compression: [yellow]{s.Compression:F3}[/]");
            if (verbose && s.Entanglement.Count > 0)
                _console.MarkupLine($"  Entangled:   {string.Join(", ", s.Entanglement)}");
        }
        else
        {
            _console.MarkupLine($"[red]Chaos injection failed:[/] {result.Error}");
        }

        return 0;
    }
}
