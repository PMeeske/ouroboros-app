using Microsoft.Extensions.Logging;
using Spectre.Console;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Services;

namespace Ouroboros.CLI.Commands.Handlers;

/// <summary>
/// Handler for the skills command.
/// </summary>
public sealed class SkillsCommandHandler
{
    private readonly ISkillsService _skillsService;
    private readonly ISpectreConsoleService _console;
    private readonly IVoiceIntegrationService _voiceService;
    private readonly ILogger<SkillsCommandHandler> _logger;

    public SkillsCommandHandler(
        ISkillsService skillsService,
        ISpectreConsoleService console,
        IVoiceIntegrationService voiceService,
        ILogger<SkillsCommandHandler> logger)
    {
        _skillsService = skillsService;
        _console = console;
        _voiceService = voiceService;
        _logger = logger;
    }

    public async Task<int> HandleAsync(
        bool list,
        string? fetch,
        bool useVoice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (useVoice)
            {
                var voiceArgs = new List<string>();
                if (list) voiceArgs.AddRange(["--list", "true"]);
                if (!string.IsNullOrEmpty(fetch)) voiceArgs.AddRange(["--fetch", fetch]);
                await _voiceService.HandleVoiceCommandAsync("skills", voiceArgs.ToArray(), cancellationToken);
                return 0;
            }

            if (list)
            {
                var skills = await _skillsService.ListSkillsAsync();
                var table = new Table();
                table.AddColumn("Name");
                table.AddColumn("Description");
                table.AddColumn("Success Rate");

                foreach (var skill in skills)
                {
                    table.AddRow(skill.Name, skill.Description, $"{skill.SuccessRate:P0}");
                }

                _console.Write(table);
            }
            else if (!string.IsNullOrEmpty(fetch))
            {
                await _console.Status().StartAsync("Fetching research...", async ctx =>
                {
                    var result = await _skillsService.FetchAndExtractSkillAsync(fetch);
                    ctx.Status = "Done";
                    _console.MarkupLine($"[green]Extracted skill:[/] {result}");
                });
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing skills command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
