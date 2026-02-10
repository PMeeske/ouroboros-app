// <copyright file="MultiModelPresetExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Services;

/// <summary>
/// Demonstrates the multi-model orchestrator preset system.
/// Shows how to use preconfigured presets with Anthropic as master
/// and Ollama for specialized sub-models.
/// </summary>
public static class MultiModelPresetExample
{
    /// <summary>
    /// Lists all available presets and their configurations.
    /// </summary>
    public static void ListPresets()
    {
        Console.WriteLine("=== Available Multi-Model Orchestrator Presets ===\n");

        foreach ((string name, MultiModelPresetConfig preset) in MultiModelPresets.All)
        {
            Console.WriteLine($"Preset: {name}");
            Console.WriteLine($"  Description: {preset.Description}");
            Console.WriteLine($"  Master Role: {preset.MasterRole}");
            Console.WriteLine($"  Default Temperature: {preset.DefaultTemperature}");
            Console.WriteLine($"  Default Max Tokens: {preset.DefaultMaxTokens}");
            Console.WriteLine($"  Timeout: {preset.TimeoutSeconds}s");
            Console.WriteLine($"  Metrics: {(preset.EnableMetrics ? "enabled" : "disabled")}");
            Console.WriteLine($"  Models ({preset.Models.Length}):");

            foreach (ModelSlotConfig slot in preset.Models)
            {
                string master = slot.Role.Equals(preset.MasterRole, StringComparison.OrdinalIgnoreCase) ? " [MASTER]" : "";
                Console.WriteLine($"    {slot.Role,-12} {slot.ProviderType,-10} {slot.ModelName}{master}");
                Console.WriteLine($"                 Tags: [{string.Join(", ", slot.Tags)}]");
                Console.WriteLine($"                 Temp: {slot.Temperature?.ToString("F1") ?? "default"}, MaxTokens: {slot.MaxTokens?.ToString() ?? "default"}, Latency: {slot.AvgLatencyMs}ms");
            }

            Console.WriteLine();
        }

        Console.WriteLine("=== List Complete ===\n");
    }

    /// <summary>
    /// Demonstrates building an orchestrator from the Anthropic+Ollama preset.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAnthropicOllamaPresetExample()
    {
        Console.WriteLine("=== Multi-Model Preset: Anthropic + Ollama ===\n");
        Console.WriteLine("This preset uses Anthropic Claude as the master orchestrator");
        Console.WriteLine("with local Ollama models for specialized sub-tasks.\n");

        MultiModelPresetConfig preset = MultiModelPresets.AnthropicMasterOllamaSub;

        Console.WriteLine($"Preset: {preset.Name}");
        Console.WriteLine($"Master: {preset.MasterRole}\n");

        try
        {
            // Create models from preset
            Dictionary<string, IChatCompletionModel> models = MultiModelPresetFactory.CreateModels(preset);
            Console.WriteLine($"Created {models.Count} model instances:\n");

            foreach ((string role, IChatCompletionModel _) in models)
            {
                ModelSlotConfig? slot = Array.Find(preset.Models, s => s.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
                if (slot is not null)
                {
                    Console.WriteLine($"  {role,-12} -> {slot.ProviderType}:{slot.ModelName}");
                }
            }

            Console.WriteLine();

            // Build orchestrator
            ToolRegistry tools = ToolRegistry.CreateDefault();
            OrchestratorBuilder builder = new OrchestratorBuilder(tools, preset.MasterRole);

            foreach (ModelSlotConfig slot in preset.Models.Where(s => models.ContainsKey(s.Role)))
            {
                var model = models[slot.Role];

                ModelType modelType = slot.Role.ToLowerInvariant() switch
                {
                    "coder" => ModelType.Code,
                    "reasoner" => ModelType.Reasoning,
                    _ => ModelType.General,
                };

                int maxTokens = slot.MaxTokens ?? preset.DefaultMaxTokens;
                builder = builder.WithModel(slot.Role, model, modelType, slot.Tags, maxTokens: maxTokens, avgLatencyMs: slot.AvgLatencyMs);
            }

            builder = builder.WithMetricTracking(preset.EnableMetrics);
            OrchestratedChatModel orchestrator = builder.Build();

            Console.WriteLine("Orchestrator built successfully.\n");

            // Test prompts that should route to different models
            (string category, string prompt)[] testPrompts =
            [
                ("General (-> Anthropic)", "What are the key principles of good software architecture?"),
                ("Coding (-> Ollama coder)", "Write a C# method that implements binary search on a sorted array."),
                ("Reasoning (-> Ollama reasoner)", "Analyze the trade-offs between microservices and monolithic architectures."),
            ];

            foreach ((string category, string prompt) in testPrompts)
            {
                Console.WriteLine($"--- {category} ---");
                Console.WriteLine($"Prompt: {prompt}");

                try
                {
                    string response = await orchestrator.GenerateTextAsync(prompt);
                    Console.WriteLine($"Response: {response[..Math.Min(200, response.Length)]}...\n");
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Connection refused"))
                    {
                        Console.WriteLine("Ollama not running - skipping\n");
                    }
                    else if (ex.Message.Contains("API key"))
                    {
                        Console.WriteLine("Anthropic API key not set - skipping\n");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            Console.WriteLine($"Note: {ex.Message}");
            Console.WriteLine("Set ANTHROPIC_API_KEY environment variable to run this example with live models.\n");
            Console.WriteLine("Falling back to preset listing...\n");
            ListPresets();
        }

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates creating a custom preset at runtime.
    /// </summary>
    public static void RunCustomPresetExample()
    {
        Console.WriteLine("=== Custom Multi-Model Preset ===\n");
        Console.WriteLine("You can create custom presets by constructing MultiModelPresetConfig directly.\n");

        var customPreset = new MultiModelPresetConfig
        {
            Name = "my-custom-preset",
            Description = "A custom preset for my specific workflow",
            MasterRole = "general",
            DefaultTemperature = 0.5,
            DefaultMaxTokens = 4096,
            TimeoutSeconds = 90,
            EnableMetrics = true,
            Models =
            [
                new ModelSlotConfig
                {
                    Role = "general",
                    ModelName = "claude-sonnet-4-20250514",
                    ProviderType = "anthropic",
                    ApiKeyEnvVar = "ANTHROPIC_API_KEY",
                    Tags = ["general", "planning"],
                    AvgLatencyMs = 2000,
                },
                new ModelSlotConfig
                {
                    Role = "coder",
                    ModelName = "codellama:13b",
                    ProviderType = "ollama",
                    Tags = ["code", "programming"],
                    AvgLatencyMs = 1500,
                },
            ],
        };

        Console.WriteLine($"Created custom preset: {customPreset.Name}");
        Console.WriteLine($"  Models: {customPreset.Models.Length}");

        foreach (ModelSlotConfig slot in customPreset.Models)
        {
            Console.WriteLine($"    {slot.Role} -> {slot.ProviderType}:{slot.ModelName}");
        }

        Console.WriteLine("\nThis preset can be used with MultiModelPresetFactory.CreateModels()");
        Console.WriteLine("to instantiate live model instances for the orchestrator.\n");

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Runs all multi-model preset examples.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("MULTI-MODEL ORCHESTRATOR PRESETS - EXAMPLES");
        Console.WriteLine(new string('=', 70) + "\n");

        ListPresets();
        RunCustomPresetExample();
        await RunAnthropicOllamaPresetExample();

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("ALL MULTI-MODEL PRESET EXAMPLES COMPLETED!");
        Console.WriteLine(new string('=', 70) + "\n");
    }
}
