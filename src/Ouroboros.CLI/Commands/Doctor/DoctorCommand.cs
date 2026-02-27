namespace Ouroboros.CLI.Commands;

using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using Ouroboros.Application.Configuration;
using Spectre.Console;

/// <summary>
/// Diagnostic command that checks the developer environment for required
/// and optional dependencies, connectivity, and configuration.
/// </summary>
public static class DoctorCommand
{
    public static async Task RunAsync(IAnsiConsole console)
    {
        console.MarkupLine("[bold]Ouroboros Doctor[/] — checking your environment\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Check")
            .AddColumn("Status")
            .AddColumn("Details");

        // .NET SDK
        var (dotnetOk, dotnetVersion) = await CheckCommandAsync("dotnet", "--version");
        table.AddRow(
            ".NET SDK",
            dotnetOk ? "[green]OK[/]" : "[red]MISSING[/]",
            dotnetOk ? $"v{dotnetVersion.Trim()}" : "Install from https://dotnet.microsoft.com/download");

        // Ollama
        var (ollamaOk, ollamaVersion) = await CheckCommandAsync("ollama", "--version");
        table.AddRow(
            "Ollama",
            ollamaOk ? "[green]OK[/]" : "[yellow]NOT FOUND[/]",
            ollamaOk ? ollamaVersion.Trim() : "Optional — install from https://ollama.com");

        // Ollama connectivity
        if (ollamaOk)
        {
            var ollamaReachable = await CheckHttpAsync($"{DefaultEndpoints.Ollama}/api/tags");
            table.AddRow(
                "  Ollama API",
                ollamaReachable ? "[green]REACHABLE[/]" : "[yellow]UNREACHABLE[/]",
                ollamaReachable ? DefaultEndpoints.Ollama : "Run 'ollama serve' to start");
        }

        // Docker
        var (dockerOk, dockerVersion) = await CheckCommandAsync("docker", "--version");
        table.AddRow(
            "Docker",
            dockerOk ? "[green]OK[/]" : "[yellow]NOT FOUND[/]",
            dockerOk ? dockerVersion.Trim() : "Optional — needed for docker-compose stack");

        // MeTTa
        var (mettaOk, _) = await CheckCommandAsync("metta", "--version");
        table.AddRow(
            "MeTTa",
            mettaOk ? "[green]OK[/]" : "[yellow]NOT FOUND[/]",
            mettaOk ? "Available" : "Optional — needed for symbolic reasoning");

        // Git submodules
        var submodulesOk = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "libs", "foundation", "src"))
                        || Directory.Exists("libs/foundation/src");
        table.AddRow(
            "Git Submodules",
            submodulesOk ? "[green]OK[/]" : "[red]MISSING[/]",
            submodulesOk ? "foundation + engine present" : "Run 'git submodule update --init --recursive'");

        // API keys (check env vars without revealing values)
        AddApiKeyRow(table, "Anthropic API Key", "ANTHROPIC_API_KEY");
        AddApiKeyRow(table, "OpenAI API Key", "OPENAI_API_KEY");
        AddApiKeyRow(table, "GitHub Token", "GITHUB_TOKEN");

        // Platform info
        table.AddRow(
            "Platform",
            "[blue]INFO[/]",
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");

        table.AddRow(
            "Runtime",
            "[blue]INFO[/]",
            RuntimeInformation.FrameworkDescription);

        console.Write(table);
        console.WriteLine();
        console.MarkupLine("[dim]Items marked [yellow]NOT FOUND[/] are optional. Items marked [red]MISSING[/] should be resolved.[/]");
    }

    private static void AddApiKeyRow(Table table, string name, string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        var isSet = !string.IsNullOrWhiteSpace(value);
        table.AddRow(
            name,
            isSet ? "[green]SET[/]" : "[yellow]NOT SET[/]",
            isSet ? $"${envVar} configured" : $"Set ${envVar} if you need this provider");
    }

    private static async Task<(bool success, string output)> CheckCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null)
                return (false, string.Empty);

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    private static async Task<bool> CheckHttpAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
