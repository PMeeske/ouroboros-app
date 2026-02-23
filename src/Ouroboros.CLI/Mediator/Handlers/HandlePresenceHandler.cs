using MediatR;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Resources;
using Spectre.Console;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="HandlePresenceRequest"/>.
/// Handles presence detection — greets user proactively if push mode is enabled.
/// </summary>
public sealed class HandlePresenceHandler : IRequestHandler<HandlePresenceRequest, Unit>
{
    private readonly OuroborosAgent _agent;

    public HandlePresenceHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<Unit> Handle(HandlePresenceRequest request, CancellationToken ct)
    {
        var evt = request.Event;

        System.Diagnostics.Debug.WriteLine($"[Presence] User presence detected via {evt.Source} (confidence={evt.Confidence:P0})");

        // Only proactively greet if:
        // 1. Push mode is enabled
        // 2. User was previously absent (state changed)
        // 3. Haven't greeted recently (avoid spam)
        var shouldGreet = _agent.Config.EnablePush &&
                          !_agent.EmbodimentSub.UserWasPresent &&
                          (DateTime.UtcNow - _agent.EmbodimentSub.LastGreetingTime).TotalMinutes > 5 &&
                          evt.Confidence > 0.6;

        _agent.EmbodimentSub.UserWasPresent = true;

        if (shouldGreet)
        {
            _agent.EmbodimentSub.LastGreetingTime = DateTime.UtcNow;

            // Generate a contextual greeting
            var greeting = await GeneratePresenceGreetingAsync(evt);

            // Notify via AutonomousMind's proactive channel
            var autonomousMind = _agent.AutonomySub.AutonomousMind;
            if (autonomousMind != null && !autonomousMind.SuppressProactiveMessages)
            {
                // Fire proactive message event
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[rgb(148,103,189)]  \U0001f44b {Markup.Escape(greeting)}[/]");

                // Speak the greeting
                await _agent.VoiceService.WhisperAsync(greeting);
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Generates a contextual greeting when user presence is detected.
    /// </summary>
    private async Task<string> GeneratePresenceGreetingAsync(PresenceEvent evt)
    {
        var defaultGreeting = _agent.LocalizationSub.GetLocalizedString("Welcome back! I'm here if you need anything.");

        var chatModel = _agent.ModelsSub.ChatModel;
        if (chatModel == null)
        {
            return defaultGreeting;
        }

        try
        {
            var context = evt.TimeSinceLastState.HasValue
                ? $"The user was away for {evt.TimeSinceLastState.Value.TotalMinutes:F0} minutes."
                : "The user just arrived.";

            // Add language directive if culture is set
            var languageDirective = _agent.LocalizationSub.GetLanguageDirective();

            var prompt = PromptResources.GreetingGeneration(languageDirective, context);

            var greeting = await chatModel.GenerateTextAsync(prompt, CancellationToken.None);
            return greeting?.Trim() ?? defaultGreeting;
        }
        catch
        {
            return defaultGreeting;
        }
    }
}
