using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the ask command.
/// </summary>
public sealed class AskCommandHandler
{
    private readonly IAskService _askService;
    private readonly ISpectreConsoleService _console;
    private readonly IVoiceIntegrationService _voiceService;
    private readonly ILogger<AskCommandHandler> _logger;

    public AskCommandHandler(
        IAskService askService,
        ISpectreConsoleService console,
        IVoiceIntegrationService voiceService,
        ILogger<AskCommandHandler> logger)
    {
        _askService = askService;
        _console = console;
        _voiceService = voiceService;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        AskRequest request,
        bool useVoice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (useVoice)
            {
                await _voiceService.HandleVoiceCommandAsync(
                    "ask",
                    ["--question", request.Question, "--rag", request.UseRag.ToString()],
                    cancellationToken);
                return 0;
            }

            await _console.Status().StartAsync("Processing question...", async ctx =>
            {
                var result = await _askService.AskAsync(request, cancellationToken);
                ctx.Status = "Done";
                _console.MarkupLine($"[green]Answer:[/] {result}");
            });

            return 0;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error executing ask command");
            _console.MarkupLine($"[red]Error:[/] Could not reach the LLM endpoint.");
            _console.MarkupLine($"[dim]  Detail:[/] {ex.Message}");
            _console.MarkupLine("[dim]  Possible fixes:[/]");
            _console.MarkupLine("[dim]    - Ensure Ollama is running: [yellow]ollama serve[/][/]");
            _console.MarkupLine("[dim]    - Check the endpoint URL with [yellow]--endpoint[/][/]");
            _console.MarkupLine("[dim]    - Run [yellow]dotnet run -- doctor[/] to diagnose your environment[/]");
            return 1;
        }
        catch (TaskCanceledException)
        {
            _console.MarkupLine("[yellow]Request timed out.[/] The model may be loading or the endpoint is slow.");
            _console.MarkupLine("[dim]  Try again, or increase the timeout in appsettings.json (Pipeline:LlmProvider:RequestTimeoutSeconds).[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ask command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            _console.MarkupLine("[dim]  Run [yellow]dotnet run -- doctor[/] to check your environment.[/]");
            return 1;
        }
    }
}