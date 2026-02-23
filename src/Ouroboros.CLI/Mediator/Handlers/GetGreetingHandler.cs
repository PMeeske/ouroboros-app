using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="GetGreetingRequest"/>.
/// Generates a personalized LLM greeting at session start, with fallback to canned greetings.
/// </summary>
public sealed class GetGreetingHandler : IRequestHandler<GetGreetingRequest, string>
{
    private readonly OuroborosAgent _agent;

    public GetGreetingHandler(OuroborosAgent agent) => _agent = agent;

    private static readonly string[] GreetingStyles =
    [
        "playfully teasing about the time since last session",
        "genuinely curious about what project they're working on",
        "warmly welcoming like an old friend",
        "subtly competitive, eager to tackle a challenge together",
        "contemplative and philosophical",
        "energetically enthusiastic about the day ahead",
        "calm and focused, ready for serious work",
        "slightly mysterious, hinting at discoveries to share"
    ];

    private static readonly string[] GreetingMoods =
    [
        "witty and sharp",
        "warm and inviting",
        "playfully sarcastic",
        "thoughtfully curious",
        "quietly confident",
        "gently encouraging"
    ];

    public async Task<string> Handle(GetGreetingRequest request, CancellationToken ct)
    {
        var persona = _agent.VoiceService.ActivePersona;
        var hour = DateTime.Now.Hour;
        var localization = _agent.LocalizationSub;
        var timeOfDay = localization.GetLocalizedTimeOfDay(hour);

        var style = GreetingStyles[Random.Shared.Next(GreetingStyles.Length)];
        var mood = GreetingMoods[Random.Shared.Next(GreetingMoods.Length)];
        var dayOfWeek = DateTime.Now.DayOfWeek;
        var uniqueSeed = Guid.NewGuid().GetHashCode() % 10000;

        var languageDirective = localization.GetLanguageDirective();

        var prompt = PromptResources.PersonaGreeting(
            languageDirective, persona.Name, timeOfDay,
            dayOfWeek.ToString(), style, mood, uniqueSeed.ToString());

        try
        {
            var llm = _agent.ModelsSub.Llm;
            if (llm?.InnerModel == null)
                return GetRandomFallbackGreeting(hour, localization);

            var response = await llm.InnerModel.GenerateTextAsync(prompt);
            return response.Trim().Trim('"');
        }
        catch
        {
            return GetRandomFallbackGreeting(hour, localization);
        }
    }

    private static string GetRandomFallbackGreeting(int hour, Subsystems.LocalizationSubsystem localization)
    {
        var timeOfDay = localization.GetLocalizedTimeOfDay(hour);
        var fallbacks = localization.GetLocalizedFallbackGreetings(timeOfDay);
        return fallbacks[Random.Shared.Next(fallbacks.Length)];
    }
}
