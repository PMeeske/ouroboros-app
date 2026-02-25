using System.Collections.Concurrent;
using System.Reflection;

namespace Ouroboros.CLI.Resources;

/// <summary>
/// Loads prompt templates from embedded resource files under Resources/Prompts/.
/// Templates use {Placeholder} syntax for string interpolation via <see cref="Format"/>.
/// </summary>
internal static class PromptResources
{
    private static readonly Assembly Assembly = typeof(PromptResources).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>Loads a raw template by name (without extension).</summary>
    public static string Load(string name)
        => Cache.GetOrAdd(name, static n =>
        {
            var resourceName = $"Ouroboros.CLI.Resources.Prompts.{n}.txt";
            using var stream = Assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded prompt resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

    /// <summary>Loads a template and replaces {Key} placeholders with values.</summary>
    public static string Format(string name, params (string Key, string? Value)[] replacements)
    {
        var template = Load(name);
        foreach (var (key, value) in replacements)
            template = template.Replace($"{{{key}}}", value ?? "");
        return template;
    }

    // ── Pre-defined accessors for commonly used prompts ──

    public static string ToolIntegration(string originalResponse, string toolResults)
        => Format("ToolIntegration",
            ("OriginalResponse", originalResponse),
            ("ToolResults", toolResults));

    public static string SummarizeToolOutput(string rawOutput)
        => Format("SummarizeToolOutput", ("RawOutput", rawOutput));

    public static string GreetingGeneration(string languageDirective, string context)
        => Format("GreetingGeneration",
            ("LanguageDirective", languageDirective),
            ("Context", context));

    public static string NeuronCodeGen(
        string name, string description, string rationale, string type,
        string subscribedTopics, string capabilities, string messageHandlers, string tickBehavior)
        => Format("NeuronCodeGen",
            ("Name", name), ("Description", description),
            ("Rationale", rationale), ("Type", type),
            ("SubscribedTopics", subscribedTopics), ("Capabilities", capabilities),
            ("MessageHandlers", messageHandlers), ("TickBehavior", tickBehavior));

    public static string PersonaGreeting(
        string languageDirective, string personaName, string timeOfDay,
        string dayOfWeek, string style, string mood, string seed)
        => Format("PersonaGreeting",
            ("LanguageDirective", languageDirective), ("PersonaName", personaName),
            ("TimeOfDay", timeOfDay), ("DayOfWeek", dayOfWeek),
            ("Style", style), ("Mood", mood), ("Seed", seed));

    public static string LanguageDirective(string languageName, string culture)
        => Format("LanguageDirective",
            ("LanguageName", languageName), ("Culture", culture));

    public static string ToolAvailability(int toolCount)
        => Format("ToolAvailability", ("ToolCount", toolCount.ToString()));

    public static string ToolUsageInstruction(
        string primarySearchTool, string primarySearchDesc,
        string searchExample, string otherTools)
        => Format("ToolUsageInstruction",
            ("PrimarySearchTool", primarySearchTool),
            ("PrimarySearchDesc", primarySearchDesc),
            ("SearchExample", searchExample),
            ("OtherTools", otherTools));

    public static string SmartToolHint(string relevantTools, string reasoning)
        => Format("SmartToolHint",
            ("RelevantTools", relevantTools),
            ("Reasoning", reasoning));
}
