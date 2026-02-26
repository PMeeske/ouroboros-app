// <copyright file="GuidedSetup.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Setup;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;

/// <summary>
/// Provides guided setup for the local development environment.
/// </summary>
public static class GuidedSetup
{
    /// <summary>
    /// Runs the guided setup based on the provided options.
    /// </summary>
    /// <param name="options">The setup options.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAsync(SetupOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Welcome to the Ouroboros Guided Setup Wizard"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("This utility will help you configure your local development environment");
        AnsiConsole.WriteLine("for running AI pipelines with small, efficient models locally.");
        AnsiConsole.WriteLine();

        if (options.All)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Running complete setup with all components..."));
            AnsiConsole.WriteLine();
            await InstallOllamaAsync();
            await ConfigureAuthAsync();
            await InstallMeTTaAsync();
            await InstallVectorStoreAsync();
            await ShowQuickStartExamplesAsync();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✨ All setup steps completed successfully!"));
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("You're ready to start using Ouroboros.");
            AnsiConsole.MarkupLine($"Run {OuroborosTheme.Accent("dotnet run -- --help")} to see all available commands.");
            return;
        }

        bool anyStepRan = false;

        if (options.InstallOllama)
        {
            await InstallOllamaAsync();
            anyStepRan = true;
        }

        if (options.ConfigureAuth)
        {
            await ConfigureAuthAsync();
            anyStepRan = true;
        }

        if (options.InstallMeTTa)
        {
            await InstallMeTTaAsync();
            anyStepRan = true;
        }

        if (options.InstallVectorStore)
        {
            await InstallVectorStoreAsync();
            anyStepRan = true;
        }

        if (anyStepRan)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✨ Selected setup steps completed successfully!"));
            await ShowQuickStartExamplesAsync();
        }
        else
        {
            AnsiConsole.WriteLine("No setup options specified. Use --help to see available options.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Quick setup commands:"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("--all")}              Run complete setup");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("--ollama")}           Install Ollama for local LLMs");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("--auth")}             Configure external provider authentication");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("--metta")}            Install MeTTa symbolic reasoning engine");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("--vector-store")}     Setup local vector database");
        }
    }

    private static async Task InstallOllamaAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Ollama Installation Guide"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        if (IsCommandAvailable("ollama"))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✓ Ollama is already installed on your system!"));

            // Check if Ollama is running
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ollama",
                        ArgumentList = { "list" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(OuroborosTheme.Accent("Currently installed models:"));
                    AnsiConsole.WriteLine(output);

                    if (!output.Contains("phi3") && !output.Contains("qwen") && !output.Contains("llama"))
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("Recommended small models for efficient orchestration:"));
                        AnsiConsole.MarkupLine($"   {OuroborosTheme.Accent("phi3:mini")} (2.3GB) - Fast, general-purpose reasoning");
                        AnsiConsole.MarkupLine($"   {OuroborosTheme.Accent("qwen2.5:3b")} (2GB) - Excellent for complex tasks");
                        AnsiConsole.MarkupLine($"   {OuroborosTheme.Accent("deepseek-coder:1.3b")} (800MB) - Specialized for coding");
                        AnsiConsole.MarkupLine($"   {OuroborosTheme.Accent("tinyllama")} (637MB) - Ultra-light for simple tasks");
                        AnsiConsole.WriteLine();

                        if (PromptYesNo("Would you like guidance on pulling recommended models?"))
                        {
                            AnsiConsole.WriteLine();
                            AnsiConsole.MarkupLine(OuroborosTheme.Accent("To install recommended models, run:"));
                            AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull phi3:mini")}");
                            AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull qwen2.5:3b")}");
                            AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull deepseek-coder:1.3b")}");
                        }
                    }
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn("Ollama is installed but not running."));
                    AnsiConsole.MarkupLine($"Please start Ollama by running: {OuroborosTheme.Dim("ollama serve")}");
                }
            }
            catch
            {
                AnsiConsole.WriteLine();
                var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} Could not verify Ollama status.[/]");
            }

            return;
        }

        AnsiConsole.WriteLine("Ollama is not found in your PATH.");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Ollama is a lightweight runtime for running LLMs locally.");
        AnsiConsole.WriteLine("It enables efficient orchestration with small, specialized models.");
        AnsiConsole.WriteLine();

        if (!PromptYesNo("Do you want to install Ollama now?"))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("Skipping Ollama installation."));
            return;
        }

        string url = "https://ollama.com/download";
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{OuroborosTheme.Accent("Download Ollama from:")} {Markup.Escape(url)}");
        AnsiConsole.WriteLine();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Installation steps for Windows:"));
            AnsiConsole.WriteLine("1. Download the installer from the website");
            AnsiConsole.WriteLine("2. Run the installer (it will add Ollama to your PATH)");
            AnsiConsole.WriteLine("3. Restart your terminal");
            AnsiConsole.MarkupLine($"4. Verify by running: {OuroborosTheme.Dim("ollama --version")}");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Installation steps for Linux:"));
            AnsiConsole.WriteLine("Run this command in your terminal:");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("curl -fsSL https://ollama.com/install.sh | sh")}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Then verify by running: {OuroborosTheme.Dim("ollama --version")}");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Installation steps for macOS:"));
            AnsiConsole.WriteLine("1. Download Ollama.app from the website");
            AnsiConsole.WriteLine("2. Move it to your Applications folder");
            AnsiConsole.WriteLine("3. Open Ollama.app (it will add the CLI to your PATH)");
            AnsiConsole.MarkupLine($"4. Verify by running: {OuroborosTheme.Dim("ollama --version")}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("After installation, recommended first steps:"));
        AnsiConsole.MarkupLine($"1. Start Ollama: {OuroborosTheme.Dim("ollama serve")} (or it may start automatically)");
        AnsiConsole.MarkupLine($"2. Pull a small model: {OuroborosTheme.Dim("ollama pull phi3:mini")}");
        AnsiConsole.MarkupLine($"3. Test it: {OuroborosTheme.Dim("ollama run phi3:mini \"Hello!\"")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("For efficient multi-model orchestration, consider installing:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull qwen2.5:3b")}         # General reasoning");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull deepseek-coder:1.3b")} # Code tasks");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("ollama pull phi3:mini")}          # Quick responses");

        await Task.Delay(2000); // Give user time to read
    }

    private static Task ConfigureAuthAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("External Provider Authentication"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("To use remote providers like OpenAI or Ollama Cloud, you need to set environment variables.");
        AnsiConsole.WriteLine("You can set these in your system, or create a '.env' file in the project root.");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Accent("Example for an OpenAI-compatible endpoint:"));
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_ENDPOINT=\"https://api.example.com/v1\"")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_API_KEY=\"your-api-key\"")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_ENDPOINT_TYPE=\"openai\"")}");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Accent("Example for Ollama Cloud:"));
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_ENDPOINT=\"https://ollama.cloud.ai\"")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_API_KEY=\"your-ollama-cloud-key\"")}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim("CHAT_ENDPOINT_TYPE=\"ollama-cloud\"")}");

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("These variables are loaded automatically when you run the CLI.");
        return Task.CompletedTask;
    }

    private static async Task InstallMeTTaAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("MeTTa Engine Installation"));
        AnsiConsole.WriteLine();
        if (IsCommandAvailable("metta"))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✓ MeTTa appears to be installed already. Skipping."));
            return;
        }

        AnsiConsole.WriteLine("The MeTTa (Meta-language for Type-Theoretic Agents) engine is not found in your PATH.");
        if (!PromptYesNo("Do you want to proceed with installation guidance for MeTTa?"))
        {
            return;
        }

        AnsiConsole.WriteLine("MeTTa is required for advanced symbolic reasoning features.");
        AnsiConsole.MarkupLine($"{OuroborosTheme.Accent("Installation instructions:")} TrueAGI Hyperon-Experimental repository:");
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("https://github.com/trueagi-io/hyperon-experimental"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Please follow their instructions to build and install the 'metta' executable and ensure it is in your system's PATH.");
        await Task.Delay(1000);
    }

    private static async Task InstallVectorStoreAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Local Vector Store Installation (Qdrant)"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("For local vector persistence, this project can use Qdrant.");
        AnsiConsole.WriteLine("The easiest way to run Qdrant is with Docker.");

        if (!IsCommandAvailable("docker"))
        {
            var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ Docker is not found. Please install Docker Desktop from: https://www.docker.com/products/docker-desktop/[/]");
            return;
        }

        if (!PromptYesNo("Do you want to see the command to run a local Qdrant container?"))
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.Accent("Run the following Docker command to start a Qdrant instance:"));
        AnsiConsole.MarkupLine($"{OuroborosTheme.Dim("docker run -p 6333:6333 -p 6334:6334 \\")}");
        AnsiConsole.MarkupLine($"{OuroborosTheme.Dim("  -v $(pwd)/qdrant_storage:/qdrant/storage:z \\")}");
        AnsiConsole.MarkupLine($"{OuroborosTheme.Dim("  qdrant/qdrant")}");

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("This will store vector data in a 'qdrant_storage' directory in your current folder.");
        await Task.Delay(1000);
    }

    /// <summary>
    /// Prompts the user with a yes/no question.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <returns><c>true</c> if the user answers yes, <c>false</c> otherwise.</returns>
    public static bool PromptYesNo(string prompt)
    {
        AnsiConsole.Markup($"{Markup.Escape(prompt)} {OuroborosTheme.GoldText("(y/n):")} ");
        string? response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes";
    }

    private static bool IsCommandAvailable(string command)
    {
        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static Task ShowQuickStartExamplesAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Quick Start Examples"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Here are some commands to get you started:");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("1. Ask a simple question:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("dotnet run -- ask -q \"What is functional programming?\"")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("2. Use the orchestrator with small models for complex tasks:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("dotnet run -- orchestrator --goal \"Explain monadic composition\" \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  --model \"phi3\" --show-metrics")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("3. Run a pipeline with iterative refinement:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("dotnet run -- pipeline \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  -d \"SetTopic('AI Safety') | UseDraft | UseCritique | UseImprove\"")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("4. Use MeTTa for symbolic reasoning:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("dotnet run -- metta --goal \"Analyze data patterns\" --plan-only")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("5. Chain operations efficiently (orchestration example):"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("dotnet run -- pipeline \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  -d \"SetTopic('Code Review') | UseDraft | UseCritique | UseImprove\" \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  --router auto \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  --general-model phi3 \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  --coder-model deepseek-coder:1.3b \\")}");
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Dim("  --reason-model qwen2.5:3b")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(OuroborosTheme.GoldText("Tips for efficient orchestration with small models:"));
        AnsiConsole.MarkupLine($"   {OuroborosTheme.Accent("--router auto")} to automatically select the best model for each task");
        AnsiConsole.MarkupLine($"   Combine specialized small models ({OuroborosTheme.Accent("phi3")}, {OuroborosTheme.Accent("qwen")}, {OuroborosTheme.Accent("deepseek")}) for complex workflows");
        AnsiConsole.MarkupLine($"   Enable {OuroborosTheme.Accent("--trace")} to see which model handles each pipeline step");
        AnsiConsole.MarkupLine($"   Use {OuroborosTheme.Accent("--show-metrics")} to track performance and optimize model selection");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"For more information, see the README or run {OuroborosTheme.Dim("dotnet run -- --help")}");
        AnsiConsole.WriteLine();
        return Task.CompletedTask;
    }
}
