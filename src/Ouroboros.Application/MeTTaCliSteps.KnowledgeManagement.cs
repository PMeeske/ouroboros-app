using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Application;

public static partial class MeTTaCliSteps
{
    /// <summary>
    /// Creates a concept from the current context/output and stores it as MeTTa atoms.
    /// Usage: MeTTaConcept('concept-name') - converts s.Output or s.Context to MeTTa atoms
    /// </summary>
    [PipelineToken("MeTTaConcept", "Concept", "Reify")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaConcept(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string conceptName = ParseString(args);
            if (string.IsNullOrWhiteSpace(conceptName))
            {
                conceptName = $"concept-{Guid.NewGuid().ToString()[..8]}";
            }

            string content = !string.IsNullOrWhiteSpace(s.Output) ? s.Output : s.Context;
            if (string.IsNullOrWhiteSpace(content))
            {
                Console.WriteLine("[metta] No content to conceptualize. Run a previous step first.");
                return s;
            }

            // Create main concept atom
            await s.MeTTaEngine.AddFactAsync($"(: {conceptName} Concept)");

            // Extract key terms and create relations
            var terms = ExtractKeyTerms(content);
            foreach (var term in terms.Take(10))
            {
                await s.MeTTaEngine.AddFactAsync($"(has-term {conceptName} {term})");
            }

            // Store content summary
            var summary = content.Length > 200 ? content[..200] + "..." : content;
            summary = summary.Replace("\"", "\\\"").Replace("\n", " ");
            await s.MeTTaEngine.AddFactAsync($"(content {conceptName} \"{summary}\")");

            // Add timestamp
            await s.MeTTaEngine.AddFactAsync($"(created-at {conceptName} \"{DateTime.UtcNow:O}\")");

            s.Output = $"Concept '{conceptName}' created with {terms.Count} terms";
            s.Branch = s.Branch.WithIngestEvent($"metta:concept:create:{conceptName}", terms.Take(5).ToArray());
            if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");

            return s;
        };

    /// <summary>
    /// Links two concepts or atoms together.
    /// Usage: MeTTaLink('source', 'target', 'relation') or MeTTaLink('concept1 -> concept2')
    /// </summary>
    [PipelineToken("MeTTaLink", "Link", "Connect")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaLink(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string linkSpec = ParseString(args);
            if (string.IsNullOrWhiteSpace(linkSpec))
            {
                Console.WriteLine("[metta] Link specification required. Usage: MeTTaLink('source', 'target', 'relation')");
                return s;
            }

            string source, target, relation;

            // Parse arrow notation: "concept1 -> concept2" or "concept1 -relation-> concept2"
            var arrowMatch = ArrowNotationRegex().Match(linkSpec);
            if (arrowMatch.Success)
            {
                source = arrowMatch.Groups[1].Value;
                relation = string.IsNullOrWhiteSpace(arrowMatch.Groups[2].Value) ? "relates-to" : arrowMatch.Groups[2].Value;
                target = arrowMatch.Groups[3].Value;
            }
            else
            {
                // Parse comma notation: "source, target, relation"
                var parts = linkSpec.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
                if (parts.Length < 2)
                {
                    Console.WriteLine("[metta] Need at least source and target. Usage: MeTTaLink('src', 'tgt', 'rel')");
                    return s;
                }
                source = parts[0];
                target = parts[1];
                relation = parts.Length > 2 ? parts[2] : "relates-to";
            }

            var linkFact = $"({relation} {source} {target})";
            var result = await s.MeTTaEngine.AddFactAsync(linkFact);
            result.Match(
                _ =>
                {
                    s.Output = $"Link created: {source} -{relation}-> {target}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:link:create", new[] { source, relation, target });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to create link: {error}");
                });

            return s;
        };

    /// <summary>
    /// Generates MeTTa atoms from LLM analysis of content.
    /// Usage: MeTTaGenerate('topic') - asks LLM to generate relevant atoms
    /// </summary>
    [PipelineToken("MeTTaGenerate", "Generate", "Atomize")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaGenerate(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null || s.Llm == null)
            {
                Console.WriteLine("[metta] Requires both MeTTa engine and LLM");
                return s;
            }

            string topic = ParseString(args);
            if (string.IsNullOrWhiteSpace(topic))
            {
                topic = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Output;
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.WriteLine("[metta] Topic required. Usage: MeTTaGenerate('topic')");
                return s;
            }

            // Ask LLM to generate MeTTa atoms
            var prompt = $@"Generate MeTTa atoms for the topic: {topic}

Output ONLY valid MeTTa expressions, one per line. Use these patterns:
- Type declaration: (: name Type)
- Fact/relation: (relation subject object)
- Rule: (= (head $x) (body $x))
- Property: (has-property entity property value)

Example output for 'web search tool':
(: web-search Tool)
(capability web-search information-retrieval)
(has-property web-search speed fast)
(= (use-for-query $q) (if (contains $q search) web-search None))

Generate 5-10 relevant atoms:";

            var response = await s.Llm.InnerModel.GenerateTextAsync(prompt);

            // Parse and add each atom
            var lines = response.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("(") && l.EndsWith(")"))
                .ToList();

            int added = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("(="))
                {
                    await s.MeTTaEngine.ApplyRuleAsync(line);
                }
                else
                {
                    await s.MeTTaEngine.AddFactAsync(line);
                }
                added++;
            }

            s.Output = $"Generated {added} MeTTa atoms for '{topic}':\n{string.Join("\n", lines)}";
            s.Branch = s.Branch.WithIngestEvent($"metta:generate:{topic[..Math.Min(20, topic.Length)]}", lines.Take(5).ToArray());
            if (s.Trace) Console.WriteLine($"[metta] ✓ Generated {added} atoms");

            return s;
        };

    /// <summary>
    /// Introspects the MeTTa knowledge base and shows statistics.
    /// Usage: MeTTaIntrospect or MeTTaIntrospect('types') or MeTTaIntrospect('facts')
    /// </summary>
    [PipelineToken("MeTTaIntrospect", "Introspect", "KBStatus")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaIntrospect(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string filter = ParseString(args).ToLowerInvariant();

            var sb = new StringBuilder();
            sb.AppendLine("=== MeTTa Knowledge Base ===");

            // Query for types
            if (string.IsNullOrWhiteSpace(filter) || filter == "types")
            {
                var typesResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self (: $x $type) ($x $type))");
                typesResult.Match(
                    types => sb.AppendLine($"\nTypes:\n{types}"),
                    _ => sb.AppendLine("\nTypes: (none)"));
            }

            // Query for facts/relations
            if (string.IsNullOrWhiteSpace(filter) || filter == "facts" || filter == "relations")
            {
                var factsResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self ($rel $a $b) ($rel $a $b))");
                factsResult.Match(
                    facts => sb.AppendLine($"\nFacts:\n{facts}"),
                    _ => sb.AppendLine("\nFacts: (none)"));
            }

            // Query for concepts
            if (string.IsNullOrWhiteSpace(filter) || filter == "concepts")
            {
                var conceptsResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self (: $x Concept) $x)");
                conceptsResult.Match(
                    concepts => sb.AppendLine($"\nConcepts:\n{concepts}"),
                    _ => sb.AppendLine("\nConcepts: (none)"));
            }

            sb.AppendLine("============================");

            s.Output = sb.ToString();
            if (s.Trace) Console.WriteLine(s.Output);

            return s;
        };

    /// <summary>
    /// Resets the MeTTa knowledge base.
    /// Usage: MeTTaReset
    /// </summary>
    [PipelineToken("MeTTaReset", "Reset", "ClearKB")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaReset(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null) return s;

            var result = await s.MeTTaEngine.ResetAsync();
            result.Match(
                _ =>
                {
                    s.Output = "MeTTa knowledge base reset";
                    s.Branch = s.Branch.WithIngestEvent("metta:reset", Array.Empty<string>());
                    if (s.Trace) Console.WriteLine("[metta] ✓ Knowledge base reset");
                },
                error => Console.WriteLine($"[metta] ✗ Reset failed: {error}"));

            return s;
        };

    [System.Text.RegularExpressions.GeneratedRegex(@"(\S+)\s*-(\w*)-?>\s*(\S+)")]
    private static partial System.Text.RegularExpressions.Regex ArrowNotationRegex();
}
