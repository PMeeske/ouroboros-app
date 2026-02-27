using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Application;

public static partial class MeTTaCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // METTA ATOM CREATION & MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a MeTTa atom (symbol) and adds it to the knowledge base.
    /// Usage: MeTTaAtom('name') or MeTTaAtom('name', 'type') or MeTTaAtom('(concept my-idea)')
    /// </summary>
    [PipelineToken("MeTTaAtom", "Atom", "Symbol")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaAtom(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            var parsed = ParseAtomArgs(args);
            if (string.IsNullOrWhiteSpace(parsed.name))
            {
                Console.WriteLine("[metta] Atom name required. Usage: MeTTaAtom('name') or MeTTaAtom('name', 'type')");
                return s;
            }

            // Build the MeTTa expression
            string atomExpr;
            if (!string.IsNullOrWhiteSpace(parsed.atomType))
            {
                // Typed atom: (: name Type)
                atomExpr = $"(: {parsed.name} {parsed.atomType})";
            }
            else if (parsed.name.StartsWith("("))
            {
                // Raw expression passed directly
                atomExpr = parsed.name;
            }
            else
            {
                // Simple symbol
                atomExpr = $"(atom {parsed.name})";
            }

            var result = await s.MeTTaEngine.AddFactAsync(atomExpr);
            result.Match(
                _ =>
                {
                    s.Output = $"Atom created: {atomExpr}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:atom:create:{parsed.name}", new[] { atomExpr });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to create atom: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:atom:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Creates a MeTTa fact (relation between atoms).
    /// Usage: MeTTaFact('(relation subject object)') or MeTTaFact('capability', 'tool_search', 'information-retrieval')
    /// </summary>
    [PipelineToken("MeTTaFact", "Fact", "Assert")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaFact(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string fact = ParseString(args);
            if (string.IsNullOrWhiteSpace(fact))
            {
                Console.WriteLine("[metta] Fact expression required. Usage: MeTTaFact('(relation arg1 arg2)')");
                return s;
            }

            // If not wrapped in parens, try to build a tuple
            if (!fact.StartsWith("("))
            {
                var parts = fact.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
                fact = parts.Length switch
                {
                    2 => $"({parts[0]} {parts[1]})",
                    3 => $"({parts[0]} {parts[1]} {parts[2]})",
                    _ => $"({string.Join(" ", parts)})"
                };
            }

            var result = await s.MeTTaEngine.AddFactAsync(fact);
            result.Match(
                _ =>
                {
                    s.Output = $"Fact asserted: {fact}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:fact:assert:{fact[..Math.Min(50, fact.Length)]}", new[] { fact });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to assert fact: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:fact:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Creates a MeTTa rule (implication).
    /// Usage: MeTTaRule('(= (ruleName $x) (body $x))')
    /// </summary>
    [PipelineToken("MeTTaRule", "Rule", "Implication")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaRule(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string rule = ParseString(args);
            if (string.IsNullOrWhiteSpace(rule))
            {
                Console.WriteLine("[metta] Rule expression required. Usage: MeTTaRule('(= (head $x) (body $x))')");
                return s;
            }

            var result = await s.MeTTaEngine.ApplyRuleAsync(rule);
            result.Match(
                success =>
                {
                    s.Output = $"Rule applied: {rule}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:rule:apply", new[] { rule, success });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to apply rule: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:rule:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Queries the MeTTa knowledge base.
    /// Usage: MeTTaQuery('!(match &amp;self (capability $tool $cap) ($tool $cap))')
    /// </summary>
    [PipelineToken("MeTTaQuery", "Query", "MeTTaAsk")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaQuery(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string query = ParseString(args);
            if (string.IsNullOrWhiteSpace(query))
            {
                // Use current context or output as query
                query = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Output;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[metta] Query required. Usage: MeTTaQuery('!(expression)')");
                return s;
            }

            // Ensure query starts with ! for execution
            if (!query.StartsWith("!"))
            {
                query = $"!{query}";
            }

            var result = await s.MeTTaEngine.ExecuteQueryAsync(query);
            result.Match(
                success =>
                {
                    s.Output = success;
                    s.Branch = s.Branch.WithIngestEvent($"metta:query:result", new[] { query, success });
                    if (s.Trace) Console.WriteLine($"[metta] Query result: {success}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Query failed: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:query:{error}", Array.Empty<string>());
                });

            return s;
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    private static async Task EnsureMeTTaEngineAsync(CliPipelineState s)
    {
        if (s.MeTTaEngine == null)
        {
            s.MeTTaEngine = new SubprocessMeTTaEngine();
            await new MottoSteps.MottoInitializeStep(s.MeTTaEngine).ExecuteAsync(Unit.Value);
        }
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        // Simple quote stripping if needed, similar to CliSteps.ParseString
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2) return arg[1..^1];
        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2) return arg[1..^1];
        return arg;
    }

    private static (string name, string? atomType) ParseAtomArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
            return (string.Empty, null);

        var parsed = ParseString(args);

        // Check for comma-separated: "name, Type"
        if (parsed.Contains(','))
        {
            var parts = parsed.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
            return (parts[0], parts.Length > 1 ? parts[1] : null);
        }

        // Check for space-separated with type: "name Type"
        var spaceParts = parsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (spaceParts.Length >= 2 && !parsed.StartsWith("("))
        {
            return (spaceParts[0], spaceParts[1]);
        }

        return (parsed, null);
    }

    private static List<string> ExtractKeyTerms(string content)
    {
        // Extract potential key terms from content
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract capitalized words (potential concepts)
        var capitalizedMatches = Regex.Matches(content, @"\b[A-Z][a-z]+(?:[A-Z][a-z]+)*\b");
        foreach (Match m in capitalizedMatches)
        {
            var term = m.Value.ToLowerInvariant().Replace(" ", "-");
            if (term.Length > 2) terms.Add(term);
        }

        // Extract quoted terms
        var quotedMatches = Regex.Matches(content, @"""([^""]+)""");
        foreach (Match m in quotedMatches)
        {
            var term = m.Groups[1].Value.ToLowerInvariant().Replace(" ", "-");
            if (term.Length > 2 && term.Length < 30) terms.Add(term);
        }

        // Extract hashtag-like terms
        var hashMatches = Regex.Matches(content, @"#(\w+)");
        foreach (Match m in hashMatches)
        {
            terms.Add(m.Groups[1].Value.ToLowerInvariant());
        }

        return terms.Take(20).ToList();
    }
}
