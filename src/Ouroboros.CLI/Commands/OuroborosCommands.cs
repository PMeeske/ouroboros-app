#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Microsoft.Extensions.Configuration;
using Ouroboros.Application.Tools;
using Ouroboros.Options;
using Ouroboros.CLI.Setup;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Entry point for the unified Ouroboros AI agent mode.
/// </summary>
public static class OuroborosCommands
{
    /// <summary>
    /// Runs the unified Ouroboros agent with all integrated capabilities.
    /// </summary>
    public static async Task RunOuroborosAsync(OuroborosOptions opts)
    {
        // Load and apply configuration
        var configuration = AgentBootstrapper.LoadConfiguration();
        AgentBootstrapper.ApplyConfiguration(configuration);

        // Clear console safely (may fail when output is redirected)
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Ignore - output may be redirected
        }

        PrintBanner();

        if (opts.Debug)
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        }

        try
        {
            // Create unified config from CLI options
            var config = AgentBootstrapper.CreateConfig(opts);

            // Create and initialize the unified Ouroboros agent
            await using var agent = await AgentBootstrapper.CreateAgentAsync(config);

            // Handle initial goal or question if provided
            if (!string.IsNullOrEmpty(opts.Goal))
            {
                await agent.Voice.SayAsync($"I understand your goal: {opts.Goal}. Let me plan how to accomplish this.");
                await agent.ProcessGoalAsync(opts.Goal);
            }
            else if (!string.IsNullOrEmpty(opts.Question))
            {
                await agent.ProcessQuestionAsync(opts.Question);
            }
            else if (!string.IsNullOrEmpty(opts.Dsl))
            {
                await agent.ProcessDslAsync(opts.Dsl);
            }

            // Main interaction loop
            await agent.RunAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Ouroboros] Fatal error: {ex.Message}");
            if (opts.Debug)
            {
                Console.WriteLine(ex.StackTrace);
            }
            Console.ResetColor();
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@"
   ╔═══════════════════════════════════════════════════════════════════════╗
   ║                                                                       ║
   ║       ██████╗ ██╗   ██╗██████╗  ██████╗ ██████╗  ██████╗ ██████╗  ██████╗ ███████╗  ║
   ║      ██╔═══██╗██║   ██║██╔══██╗██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗██╔═══██╗██╔════╝  ║
   ║      ██║   ██║██║   ██║██████╔╝██║   ██║██████╔╝██║   ██║██████╔╝██║   ██║███████╗  ║
   ║      ██║   ██║██║   ██║██╔══██╗██║   ██║██╔══██╗██║   ██║██╔══██╗██║   ██║╚════██║  ║
   ║      ╚██████╔╝╚██████╔╝██║  ██║╚██████╔╝██████╔╝╚██████╔╝██║  ██║╚██████╔╝███████║  ║
   ║       ╚═════╝  ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚═════╝  ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚══════╝  ║
   ║                                                                       ║
   ║          ∞ The Self-Improving AI Agent ∞                             ║
   ║                                                                       ║
   ╚═══════════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("   Full-featured mode: Voice • Skills • Tools • MeTTa • Personality • Browser");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("   Quick examples:");
        Console.WriteLine("     ouroboros                        # Interactive with voice (default)");
        Console.WriteLine("     ouroboros --text-only            # Text-only mode");
        Console.WriteLine("     ouroboros -q \"What is AI?\"       # Answer a question");
        Console.WriteLine("     ouroboros -g \"Build a website\"   # Goal-driven planning");
        Console.WriteLine("     ouroboros -d \"SetPrompt | LLM\"   # Execute pipeline DSL");
        Console.WriteLine("     ouroboros --no-browser --no-mind # Disable specific features");
        Console.ResetColor();
        Console.WriteLine();
    }
}
