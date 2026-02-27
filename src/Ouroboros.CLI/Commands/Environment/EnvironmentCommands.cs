// <copyright file="EnvironmentCommands.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.Json;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Options;
using Ouroboros.Application.Services.Reinforcement;
using Ouroboros.Examples.Environments;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// CLI commands for environment interaction and RL training.
/// </summary>
public static class EnvironmentCommands
{
    /// <summary>
    /// Executes environment commands based on options.
    /// </summary>
    public static async Task RunEnvironmentCommandAsync(EnvironmentOptions options)
    {
        var command = options.Command?.ToLowerInvariant();

        switch (command)
        {
            case "step":
                await RunSingleStepAsync(options);
                break;
            case "run":
                await RunEpisodesAsync(options);
                break;
            case "replay":
                await ReplayEpisodeAsync(options);
                break;
            default:
                PrintError($"Unknown command: {options.Command}");
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("Available commands: step, run, replay"));
                break;
        }
    }

    private static async Task RunSingleStepAsync(EnvironmentOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Environment Single Step"));
        AnsiConsole.WriteLine();

        var (environment, policy) = CreateEnvironmentAndPolicy(options);

        // Get initial state
        var stateResult = await environment.GetStateAsync();
        if (stateResult.IsFailure)
        {
            PrintError($"Error getting state: {stateResult.Error}");
            return;
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Current State:")} {Markup.Escape(FormatState(stateResult.Value))}");

        // Get available actions
        var actionsResult = await environment.GetAvailableActionsAsync();
        if (actionsResult.IsFailure)
        {
            PrintError($"Error getting actions: {actionsResult.Error}");
            return;
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Available Actions:")} {Markup.Escape(string.Join(", ", actionsResult.Value.Select(a => a.ActionType)))}");

        // Select action
        var actionResult = await policy.SelectActionAsync(stateResult.Value, actionsResult.Value);
        if (actionResult.IsFailure)
        {
            PrintError($"Error selecting action: {actionResult.Error}");
            return;
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("Selected Action:")} {Markup.Escape(actionResult.Value.ActionType)}");

        // Execute action
        var observationResult = await environment.ExecuteActionAsync(actionResult.Value);
        if (observationResult.IsFailure)
        {
            PrintError($"Error executing action: {observationResult.Error}");
            return;
        }

        var observation = observationResult.Value;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Accent("  Observation:"));
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("New State:")} {Markup.Escape(FormatState(observation.State))}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Reward:")} {observation.Reward:F2}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Terminal:")} {observation.IsTerminal}");

        if (observation.Info != null && observation.Info.Count > 0)
        {
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Info:")} {Markup.Escape(string.Join(", ", observation.Info.Select(kv => $"{kv.Key}={kv.Value}")))}");
        }
    }

    private static async Task RunEpisodesAsync(EnvironmentOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Running Episodes"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Environment:")} {Markup.Escape(options.Environment)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Policy:")} {Markup.Escape(options.Policy)}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Episodes:")} {options.Episodes}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Max Steps:")} {options.MaxSteps}");
        AnsiConsole.WriteLine();

        var (environment, policy) = CreateEnvironmentAndPolicy(options);
        var runner = new EpisodeRunner(environment, policy, options.Environment);

        var episodes = new List<Episode>();
        var metrics = new List<EpisodeMetrics>();

        for (var i = 0; i < options.Episodes; i++)
        {
            var result = await runner.RunEpisodeAsync(options.MaxSteps);

            if (result.IsFailure)
            {
                PrintError($"Episode {i + 1} failed: {result.Error}");
                continue;
            }

            var episode = result.Value;
            episodes.Add(episode);

            var episodeMetrics = EpisodeMetrics.FromEpisode(episode);
            metrics.Add(episodeMetrics);

            AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Episode {i + 1}/{options.Episodes}:")}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Steps:")} {episode.StepCount}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Reward:")} {episode.TotalReward:F2}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Success:")} {(episode.Success ? OuroborosTheme.Ok("Yes") : OuroborosTheme.Err("No"))}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Duration:")} {episode.Duration?.TotalSeconds:F2}s");

            if (options.Verbose)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("    Steps detail:"));
                foreach (var step in episode.Steps)
                {
                    AnsiConsole.MarkupLine($"      Step {step.StepNumber}: {Markup.Escape(step.Action.ActionType)} → Reward: {step.Observation.Reward:F2}");
                }
            }

            AnsiConsole.WriteLine();
        }

        // Print summary statistics
        PrintSummaryStatistics(metrics);

        // Save to file if requested
        if (!string.IsNullOrWhiteSpace(options.OutputFile))
        {
            await SaveEpisodesToFileAsync(episodes, options.OutputFile);
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"\nEpisodes saved to {options.OutputFile}"));
        }
    }

    private static async Task ReplayEpisodeAsync(EnvironmentOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputFile))
        {
            PrintError("Input file required for replay. Use -i or --input option.");
            return;
        }

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Replaying Episode"));
        AnsiConsole.WriteLine();

        try
        {
            var json = await File.ReadAllTextAsync(options.InputFile);
            var episodes = JsonSerializer.Deserialize<List<Episode>>(json);

            if (episodes == null || episodes.Count == 0)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("No episodes found in file."));
                return;
            }

            foreach (var episode in episodes)
            {
                AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Episode {episode.Id}:")}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Environment:")} {Markup.Escape(episode.EnvironmentName)}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Steps:")} {episode.StepCount}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Total Reward:")} {episode.TotalReward:F2}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Success:")} {(episode.Success ? OuroborosTheme.Ok("Yes") : OuroborosTheme.Err("No"))}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Duration:")} {episode.Duration?.TotalSeconds:F2}s");
                AnsiConsole.WriteLine();

                foreach (var step in episode.Steps)
                {
                    AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText($"Step {step.StepNumber}:")}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("State:")} {Markup.Escape(FormatState(step.State))}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Action:")} {Markup.Escape(step.Action.ActionType)}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Reward:")} {step.Observation.Reward:F2}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Next State:")} {Markup.Escape(FormatState(step.Observation.State))}");
                    AnsiConsole.MarkupLine($"      {OuroborosTheme.Accent("Terminal:")} {step.Observation.IsTerminal}");
                    AnsiConsole.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error replaying episode: {ex.Message}");
        }
    }

    private static (IEnvironmentActor Environment, IPolicy Policy) CreateEnvironmentAndPolicy(
        EnvironmentOptions options)
    {
        // Create environment
        IEnvironmentActor environment = options.Environment.ToLowerInvariant() switch
        {
            "gridworld" => new GridWorldEnvironment(options.Width, options.Height),
            _ => throw new ArgumentException($"Unknown environment: {options.Environment}")
        };

        // Create policy
        IPolicy policy = options.Policy.ToLowerInvariant() switch
        {
            "epsilon-greedy" => options.Seed.HasValue
                ? new EpsilonGreedyPolicy(options.Epsilon, options.Seed.Value)
                : new EpsilonGreedyPolicy(options.Epsilon),
            "bandit" => new BanditPolicy(),
            "random" => new RandomPolicy(options.Seed),
            _ => throw new ArgumentException($"Unknown policy: {options.Policy}")
        };

        return (environment, policy);
    }

    private static string FormatState(EnvironmentState state)
    {
        var parts = state.StateData.Select(kv => $"{kv.Key}={kv.Value}");
        return $"[{string.Join(", ", parts)}]";
    }

    private static void PrintSummaryStatistics(List<EpisodeMetrics> metrics)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Summary Statistics"));
        AnsiConsole.WriteLine();

        var table = OuroborosTheme.ThemedTable("Metric", "Value");
        table.AddRow("Total Episodes", $"{metrics.Count}");
        table.AddRow("Success Rate", $"{metrics.Count(m => m.Success) / (double)metrics.Count:P1}");
        table.AddRow("Average Reward", $"{metrics.Average(m => m.TotalReward):F2}");
        table.AddRow("Average Steps", $"{metrics.Average(m => m.StepCount):F2}");
        table.AddRow("Average Duration", $"{TimeSpan.FromTicks((long)metrics.Average(m => m.Duration.Ticks)).TotalSeconds:F2}s");
        AnsiConsole.Write(table);

        var bestEpisode = metrics.OrderByDescending(m => m.TotalReward).First();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("Best Episode:")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ID:")} {bestEpisode.EpisodeId}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Reward:")} {bestEpisode.TotalReward:F2}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Steps:")} {bestEpisode.StepCount}");
    }

    private static async Task SaveEpisodesToFileAsync(List<Episode> episodes, string filePath)
    {
        var json = JsonSerializer.Serialize(episodes, JsonDefaults.IndentedExact);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static void PrintError(string message)
    {
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Random policy for baseline comparison.
    /// </summary>
    private sealed class RandomPolicy : IPolicy
    {
        private readonly Random random;

        public RandomPolicy(int? seed = null)
        {
            this.random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public ValueTask<Result<EnvironmentAction>> SelectActionAsync(
            EnvironmentState state,
            IReadOnlyList<EnvironmentAction> availableActions,
            CancellationToken cancellationToken = default)
        {
            if (availableActions == null || availableActions.Count == 0)
            {
                return ValueTask.FromResult(
                    Result<EnvironmentAction>.Failure("No available actions"));
            }

            var index = this.random.Next(availableActions.Count);
            return ValueTask.FromResult(
                Result<EnvironmentAction>.Success(availableActions[index]));
        }

        public ValueTask<Result<Unit>> UpdateAsync(
            EnvironmentState state,
            EnvironmentAction action,
            Ouroboros.Domain.Environment.Observation observation,
            CancellationToken cancellationToken = default)
        {
            // Random policy doesn't learn
            return ValueTask.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }
}
