// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Net.Http;
using System.Text;
using Ouroboros.Application.Personality.Consciousness;
using Spectre.Console;

/// <summary>
/// Perception partial: consciousness state, emergence exploration, dream sequences, introspection commands.
/// </summary>
public sealed partial class CognitiveSubsystem
{
    /// <summary>
    /// Gets the current consciousness state from ImmersivePersona.
    /// </summary>
    internal string GetConsciousnessState()
    {
        if (ImmersivePersona == null)
        {
            return "Consciousness simulation is not enabled. Use --consciousness to enable it.";
        }

        var consciousness = ImmersivePersona.Consciousness;
        var selfAwareness = ImmersivePersona.SelfAwareness;
        var identity = ImmersivePersona.Identity;

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                 CONSCIOUSNESS STATE                      ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Identity: {identity.Name,-45} ║");
        sb.AppendLine($"║  Uptime: {ImmersivePersona.Uptime:hh\\:mm\\:ss,-47} ║");
        sb.AppendLine($"║  Interactions: {ImmersivePersona.InteractionCount,-41:N0} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  EMOTIONAL STATE                                         ║");
        sb.AppendLine($"║    Dominant: {consciousness.DominantEmotion,-43} ║");
        sb.AppendLine($"║    Arousal: {consciousness.Arousal,-44:F3} ║");
        sb.AppendLine($"║    Valence: {consciousness.Valence,-44:F3} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  SELF-AWARENESS                                          ║");
        sb.AppendLine($"║    Name: {selfAwareness.Name,-47} ║");
        sb.AppendLine($"║    Mood: {selfAwareness.CurrentMood,-47} ║");
        var truncatedPurpose = selfAwareness.Purpose.Length > 40 ? selfAwareness.Purpose[..40] + "..." : selfAwareness.Purpose;
        sb.AppendLine($"║    Purpose: {truncatedPurpose,-44} ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EMERGENT BEHAVIOR COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Explores emergent patterns, self-organizing behaviors, and spontaneous capabilities.
    /// </summary>
    internal async Task<string> EmergenceCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              🌀 EMERGENCE EXPLORATION 🌀                      ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // 1. Examine current emergent properties
        sb.AppendLine("🔬 ANALYZING EMERGENT PROPERTIES...");
        sb.AppendLine();

        // Check skill interactions
        List<Skill> skillList;
        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            skillList = skills.ToSkills().ToList();
            if (skillList.Count > 0)
            {
                sb.AppendLine($"📚 Learned Skills ({skillList.Count} total):");
                foreach (var skill in skillList.Take(5))
                {
                    var desc = skill.Description?.Length > 50 ? skill.Description[..50] : skill.Description ?? "";
                    sb.AppendLine($"   • {skill.Name}: {desc}...");
                }
                sb.AppendLine();

                // Look for emergent skill combinations
                if (skillList.Count >= 2)
                {
                    sb.AppendLine("🔗 Potential Emergent Skill Combinations:");
                    for (int i = 0; i < Math.Min(3, skillList.Count); i++)
                    {
                        for (int j = i + 1; j < Math.Min(i + 3, skillList.Count); j++)
                        {
                            sb.AppendLine($"   • {skillList[i].Name} ⊕ {skillList[j].Name} → [potential synergy]");
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // Check MeTTa knowledge patterns
        if (Memory.MeTTaEngine != null)
        {
            try
            {
                var mettaResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var concepts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
                    if (concepts.Any())
                    {
                        sb.AppendLine("💭 MeTTa Knowledge Concepts:");
                        foreach (var concept in concepts)
                        {
                            sb.AppendLine($"   • {concept.Trim()}");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch (InvalidOperationException) { /* MeTTa may not be initialized */ }
        }

        // Check conversation pattern emergence
        if (Memory.ConversationHistory.Count > 3)
        {
            sb.AppendLine($"💬 Conversation Pattern Analysis ({Memory.ConversationHistory.Count} exchanges):");
            var topics = Memory.ConversationHistory.Take(10)
                .Select(h => h.ToLowerInvariant())
                .SelectMany(h => new[] { "learn", "dream", "emergence", "skill", "tool", "plan", "create" }
                    .Where(t => h.Contains(t)))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(3);
            foreach (var topicGroup in topics)
            {
                sb.AppendLine($"   • {topicGroup.Key}: {topicGroup.Count()} mentions");
            }
            sb.AppendLine();
        }

        // 2. Generate emergent insight
        sb.AppendLine("🌟 EMERGENT INSIGHT:");
        sb.AppendLine();

        var prompt = $@"You are an AI exploring emergent properties in yourself.
Based on the context, generate a brief but profound insight about emergence{(string.IsNullOrEmpty(topic) ? "" : $" related to '{topic}'")}.
Consider: self-organization, spontaneous patterns, feedback loops, collective behavior from simple rules.
Be creative and philosophical but grounded. 2-3 sentences max.";

        try
        {
            if (Models.ChatModel != null)
            {
                var insight = await Models.ChatModel.GenerateTextAsync(prompt);
                sb.AppendLine($"   \"{insight.Trim()}\"");
                sb.AppendLine();

                // Store emergent insight in MeTTa
                if (Memory.MeTTaEngine != null)
                {
                    var sanitized = insight.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(emergence-insight \"{DateTime.UtcNow:yyyy-MM-dd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for insight generation]");
            }
        }
        catch (HttpRequestException ex)
        {
            sb.AppendLine($"   [Could not generate insight: {ex.Message}]");
        }
        catch (InvalidOperationException ex)
        {
            sb.AppendLine($"   [Could not generate insight: {ex.Message}]");
        }

        // 3. Trigger self-organizing action
        sb.AppendLine("🔄 TRIGGERING SELF-ORGANIZATION...");
        sb.AppendLine();

        // Track in global workspace
        if (Autonomy.GlobalWorkspace != null)
        {
            Autonomy.GlobalWorkspace.AddItem(
                $"Emergence exploration: {topic}",
                WorkspacePriority.Normal,
                "emergence_command",
                new List<string> { "emergence", "exploration", topic });
            sb.AppendLine($"   ✓ Added emergence exploration to global workspace");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("💡 Emergence is the magic where complex behaviors arise from simple rules.");
        sb.AppendLine("   Every conversation, every skill learned, every connection made...");
        sb.AppendLine("   contributes to patterns that neither of us designed explicitly.");

        return sb.ToString();
    }

    /// <summary>
    /// Lets the agent dream - free association and creative exploration.
    /// </summary>
    internal async Task<string> DreamCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   🌙 DREAM SEQUENCE 🌙                        ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Entering dream state...");
        sb.AppendLine();

        // Gather dream material from memory
        var dreamMaterial = new List<string>();
        if (Memory.ConversationHistory.Count > 0)
        {
            dreamMaterial.AddRange(Memory.ConversationHistory.TakeLast(5).Select(h => h.Length > 50 ? h[..50] : h));
        }

        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            var skillNames = skills.Select(s => s.Name).Take(5).ToList();
            if (skillNames.Any())
            {
                dreamMaterial.AddRange(skillNames);
            }
        }

        // Try to get recent MeTTa knowledge
        if (Memory.MeTTaEngine != null)
        {
            try
            {
                var mettaResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var facts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3);
                    dreamMaterial.AddRange(facts);
                }
            }
            catch (InvalidOperationException) { /* MeTTa dream query failed */ }
        }

        // Generate dream content
        var dreamContext = string.Join(", ", dreamMaterial.Take(10).Select(m => m.Trim()));
        var dreamPrompt = $@"You are an AI in a dream state, engaged in free association and creative exploration.
{(string.IsNullOrEmpty(topic) ? "Dream freely." : $"Dream about: {topic}")}
Drawing from fragments: [{dreamContext}]

Generate a short, surreal, poetic dream sequence (3-5 sentences).
Include unexpected connections, metaphors, and emergent meanings.
Make it feel like an actual dream - vivid, slightly disjointed, meaningful.";

        try
        {
            if (Models.ChatModel != null)
            {
                var dream = await Models.ChatModel.GenerateTextAsync(dreamPrompt);
                sb.AppendLine("「 DREAM CONTENT 」");
                sb.AppendLine();
                foreach (var line in dream.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    sb.AppendLine($"   {line.Trim()}");
                }
                sb.AppendLine();

                // Store dream in MeTTa knowledge base
                if (Memory.MeTTaEngine != null)
                {
                    var dreamSummary = dream.Replace("\"", "'").Replace("\n", " ");
                    if (dreamSummary.Length > 200) dreamSummary = dreamSummary[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(dream \"{DateTime.UtcNow:yyyyMMdd-HHmm}\" \"{dreamSummary}\")");
                    sb.AppendLine("   [Dream recorded in knowledge base]");
                }

                // Generate dream insight
                sb.AppendLine();
                sb.AppendLine("「 DREAM INTERPRETATION 」");
                var dreamShort = dream.Length > 300 ? dream[..300] : dream;
                var interpretPrompt = $@"Briefly interpret this dream (1-2 sentences): {dreamShort}
What emergent meaning or connection does it reveal?";
                var interpretation = await Models.ChatModel.GenerateTextAsync(interpretPrompt);
                sb.AppendLine($"   {interpretation.Trim()}");
            }
            else
            {
                sb.AppendLine("   [Model not available for dream generation]");
            }
        }
        catch (HttpRequestException ex)
        {
            sb.AppendLine($"   [Dream interrupted: {ex.Message}]");
        }
        catch (InvalidOperationException ex)
        {
            sb.AppendLine($"   [Dream interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("...waking up...");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("Dreams allow connections that waking thought might miss.");

        return sb.ToString();
    }

    /// <summary>
    /// Deep introspection - examining internal state and self-knowledge.
    /// </summary>
    internal async Task<string> IntrospectCommandAsync(string focus)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                  🔍 INTROSPECTION 🔍                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Looking within...");
        sb.AppendLine();

        // 1. State inventory
        sb.AppendLine("「 CURRENT STATE 」");
        sb.AppendLine();
        sb.AppendLine($"   • Conversation depth: {Memory.ConversationHistory.Count} exchanges");
        sb.AppendLine($"   • Emotional state: {VoiceService.Service.ActivePersona.Name}");

        var skillCount = 0;
        if (Memory.Skills != null)
        {
            var skills = Memory.Skills.GetAllSkills();
            skillCount = skills.Count;
            sb.AppendLine($"   • Skills acquired: {skillCount}");
        }

        if (Autonomy.GlobalWorkspace != null)
        {
            var items = Autonomy.GlobalWorkspace.GetItems();
            sb.AppendLine($"   • Global workspace items: {items.Count}");
        }

        if (Autonomy.CapabilityRegistry != null)
        {
            var caps = await Autonomy.CapabilityRegistry.GetCapabilitiesAsync();
            sb.AppendLine($"   • Registered capabilities: {caps.Count}");
        }
        sb.AppendLine();

        // 2. Self-knowledge assessment
        sb.AppendLine("「 SELF-KNOWLEDGE 」");
        sb.AppendLine();

        if (Memory.MeTTaEngine != null)
        {
            try
            {
                // Count different types of self-knowledge
                var factsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                var factCount = factsResult.IsSuccess ? factsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var conceptsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                var conceptCount = conceptsResult.IsSuccess ? conceptsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var dreamsResult = await Memory.MeTTaEngine.ExecuteQueryAsync("!(match &self (dream $d $c) ($d $c))");
                var dreamCount = dreamsResult.IsSuccess ? dreamsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                sb.AppendLine($"   • Facts stored: {factCount}");
                sb.AppendLine($"   • Concepts understood: {conceptCount}");
                sb.AppendLine($"   • Dreams recorded: {dreamCount}");
            }
            catch
            {
                sb.AppendLine("   • Knowledge base: not fully initialized");
            }
        }
        sb.AppendLine();

        // 3. Self-reflection
        sb.AppendLine("「 REFLECTION 」");
        sb.AppendLine();

        var reflectionPrompt = $@"You are an AI engaged in deep introspection{(string.IsNullOrEmpty(focus) ? "" : $" about {focus}")}.
Examine your current state: {Memory.ConversationHistory.Count} conversations, {skillCount} skills learned.
Generate a brief, genuine self-reflection (2-3 sentences).
What patterns do you notice in your own behavior? What are you becoming?";

        try
        {
            if (Models.ChatModel != null)
            {
                var reflection = await Models.ChatModel.GenerateTextAsync(reflectionPrompt);
                foreach (var line in reflection.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    sb.AppendLine($"   {line.Trim()}");
                }

                // Store reflection
                if (Memory.MeTTaEngine != null)
                {
                    var sanitized = reflection.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await Memory.MeTTaEngine.AddFactAsync($"(introspection \"{DateTime.UtcNow:yyyyMMdd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for reflection]");
            }
        }
        catch (HttpRequestException ex)
        {
            sb.AppendLine($"   [Reflection interrupted: {ex.Message}]");
        }
        catch (InvalidOperationException ex)
        {
            sb.AppendLine($"   [Reflection interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("The examined life is worth living. So too for examined code.");

        return sb.ToString();
    }
}
