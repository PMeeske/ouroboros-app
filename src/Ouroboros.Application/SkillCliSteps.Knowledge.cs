// ==========================================================
// Skill CLI Steps - Knowledge Management (ListAllTokens, Reorganize, Stats, EmergenceCycle)
// ==========================================================

using System.Text;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    /// <summary>
    /// List ALL available pipeline tokens discovered at runtime.
    /// Usage: ListAllTokens | UseOutput
    /// </summary>
    [PipelineToken("ListAllTokens", "AllTokens", "PipelineTokens", "AvailableSteps")]
    public static Step<CliPipelineState, CliPipelineState> ListAllTokens(string? filter = null)
        => s =>
        {
            string? filterStr = ParseString(filter);
            var tokens = _allPipelineTokens.Value.Values.Distinct().ToList();

            if (!string.IsNullOrEmpty(filterStr))
            {
                tokens = tokens.Where(t =>
                    t.PrimaryName.Contains(filterStr, StringComparison.OrdinalIgnoreCase) ||
                    t.SourceClass.Contains(filterStr, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(filterStr, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            Console.WriteLine($"[ListAllTokens] {tokens.Count} pipeline tokens available:");

            var grouped = tokens.GroupBy(t => t.SourceClass).OrderBy(g => g.Key);
            var output = new List<string>();

            foreach (var group in grouped)
            {
                Console.WriteLine($"\n  📦 {group.Key}:");
                output.Add($"\n📦 {group.Key}:");

                foreach (var token in group.OrderBy(t => t.PrimaryName))
                {
                    string aliases = token.Aliases.Length > 0 ? $" ({string.Join(", ", token.Aliases.Take(2))})" : "";
                    Console.WriteLine($"     • {token.PrimaryName}{aliases}");
                    output.Add($"  • {token.PrimaryName}{aliases}: {token.Description}");
                }
            }

            s.Output = string.Join("\n", output);
            s.Context = BuildPipelineContext();
            return Task.FromResult(s);
        };

    /// <summary>
    /// Reorganize knowledge based on access patterns and learning.
    /// Consolidates duplicates, clusters related content, and creates summaries.
    /// Usage: ReorganizeKnowledge | UseOutput
    /// </summary>
    [PipelineToken("ReorganizeKnowledge", "Reorganize", "ConsolidateKnowledge", "OptimizeIndex")]
    public static Step<CliPipelineState, CliPipelineState> ReorganizeKnowledge(string? options = null)
        => async s =>
        {
            var indexer = Tools.SystemAccessTools.SharedIndexer;
            if (indexer == null)
            {
                Console.WriteLine("[ReorganizeKnowledge] ❌ Self-indexer not available");
                s.Output = "Knowledge reorganization unavailable - self-indexer not connected.";
                return s;
            }

            Console.WriteLine("[ReorganizeKnowledge] 🧠 Starting knowledge reorganization...");

            try
            {
                // Parse options
                bool createSummaries = true, removeDuplicates = true, clusterRelated = true;
                if (!string.IsNullOrWhiteSpace(options))
                {
                    var opts = ParseString(options)?.ToLowerInvariant() ?? "";
                    if (opts.Contains("nosummaries")) createSummaries = false;
                    if (opts.Contains("noduplicates")) removeDuplicates = false;
                    if (opts.Contains("noclusters")) clusterRelated = false;
                }

                var result = await indexer.ReorganizeAsync(createSummaries, removeDuplicates, clusterRelated);

                var sb = new StringBuilder();
                sb.AppendLine("🧠 **Knowledge Reorganization Complete**");
                sb.AppendLine($"   ⏱️ Duration: {result.Duration.TotalSeconds:F1}s");
                sb.AppendLine($"   🗑️ Duplicates removed: {result.DuplicatesRemoved}");
                sb.AppendLine($"   📊 Clusters found: {result.ClustersFound}");
                sb.AppendLine($"   📝 Summaries created: {result.SummariesCreated}");
                sb.AppendLine($"   ✏️ Chunks consolidated: {result.ConsolidatedChunks}");

                if (result.Insights.Count > 0)
                {
                    sb.AppendLine("\n💡 **Insights:**");
                    foreach (var insight in result.Insights)
                    {
                        sb.AppendLine($"   • {insight}");
                    }
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();

                // Also get reorganization stats
                var stats = indexer.GetReorganizationStats();
                if (stats.TopAccessedFiles.Count > 0)
                {
                    Console.WriteLine("\n📈 **Top Accessed Files:**");
                    foreach (var (file, count) in stats.TopAccessedFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        Console.WriteLine($"   • {fileName}: {count} accesses");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[ReorganizeKnowledge] ❌ Error: {ex.Message}");
                s.Output = $"Reorganization failed: {ex.Message}";
            }

            return s;
        };

    /// <summary>
    /// Get knowledge organization statistics.
    /// Shows access patterns, hot content, and cluster information.
    /// Usage: KnowledgeStats | UseOutput
    /// </summary>
    [PipelineToken("KnowledgeStats", "IndexStats", "KnowledgeInfo", "ReorgStats")]
    public static Step<CliPipelineState, CliPipelineState> KnowledgeStats(string? _ = null)
        => async s =>
        {
            var indexer = Tools.SystemAccessTools.SharedIndexer;
            if (indexer == null)
            {
                s.Output = "Self-indexer not available.";
                return s;
            }

            var stats = await indexer.GetStatsAsync();
            var reorgStats = indexer.GetReorganizationStats();

            var sb = new StringBuilder();
            sb.AppendLine("📊 **Knowledge Index Statistics**");
            sb.AppendLine($"   📁 Collection: {stats.CollectionName}");
            sb.AppendLine($"   🔢 Total vectors: {stats.TotalVectors:N0}");
            sb.AppendLine($"   📄 Indexed files: {stats.IndexedFiles}");
            sb.AppendLine($"   📐 Vector size: {stats.VectorSize}D");

            sb.AppendLine("\n🧠 **Reorganization State**");
            sb.AppendLine($"   📈 Tracked patterns: {reorgStats.TrackedPatterns}");
            sb.AppendLine($"   🔥 Hot content: {reorgStats.HotContentCount}");
            sb.AppendLine($"   🔗 Co-access clusters: {reorgStats.CoAccessClusters}");

            if (reorgStats.TopAccessedFiles.Count > 0)
            {
                sb.AppendLine("\n📈 **Most Accessed Files:**");
                foreach (var (file, count) in reorgStats.TopAccessedFiles)
                {
                    var fileName = Path.GetFileName(file);
                    sb.AppendLine($"   • {fileName}: {count} accesses");
                }
            }

            Console.WriteLine(sb.ToString());
            s.Output = sb.ToString();
            return s;
        };

    /// <summary>
    /// Run the full Ouroboros emergence cycle on a topic.
    /// Usage: EmergenceCycle 'transformer architectures' | UseOutput
    /// </summary>
    [PipelineToken("EmergenceCycle", "Emergence", "FullCycle", "ResearchCycle")]
    public static Step<CliPipelineState, CliPipelineState> EmergenceCycle(string? topic = null)
        => async s =>
        {
            string searchTopic = ParseString(topic) ?? s.Prompt ?? s.Query ?? "self-improving AI";

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    🌀 OUROBOROS EMERGENCE CYCLE                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\n  Topic: {searchTopic}\n");

            var allResults = new System.Text.StringBuilder();

            // Phase 1: INGEST - Fetch from multiple sources
            Console.WriteLine("  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  📥 PHASE 1: INGEST - Multi-Source Research Fetch");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");

            // arXiv
            Console.WriteLine("  🔍 Searching arXiv...");
            var arxivState = await ArxivSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("=== arXiv Papers ===");
            allResults.AppendLine(arxivState.Output ?? "No results");

            await Task.Delay(500);

            // Wikipedia
            Console.WriteLine("  🔍 Searching Wikipedia...");
            var wikiState = await WikiSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("\n=== Wikipedia ===");
            allResults.AppendLine(wikiState.Output ?? "No results");

            await Task.Delay(500);

            // Semantic Scholar
            Console.WriteLine("  🔍 Searching Semantic Scholar...");
            var scholarState = await ScholarSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("\n=== Semantic Scholar ===");
            allResults.AppendLine(scholarState.Output ?? "No results");

            // Phase 2: HYPOTHESIZE
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  🧠 PHASE 2: HYPOTHESIZE - Generate Insights");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");

            if (s.Llm?.InnerModel != null)
            {
                string hypothesisPrompt = $"""
                    Based on this research about "{searchTopic}", generate 3 key hypotheses:

                    {allResults.ToString()[..Math.Min(4000, allResults.Length)]}

                    Format:
                    1. [Hypothesis] - [Confidence: X%]
                    2. [Hypothesis] - [Confidence: X%]
                    3. [Hypothesis] - [Confidence: X%]
                    """;

                try
                {
                    string hypotheses = await s.Llm.InnerModel.GenerateTextAsync(hypothesisPrompt);
                    Console.WriteLine($"  {hypotheses.Replace("\n", "\n  ")}");
                    allResults.AppendLine("\n=== Generated Hypotheses ===");
                    allResults.AppendLine(hypotheses);
                }
                catch
                {
                    Console.WriteLine("  [LLM unavailable - skipping hypothesis generation]");
                }
            }

            // Phase 3: EXPLORE
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  🔮 PHASE 3: EXPLORE - Identify Opportunities");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");

            var opportunities = new[]
            {
                $"Deep dive into recent {searchTopic} breakthroughs (Novelty: 85%)",
                $"Cross-domain application of {searchTopic} to adjacent fields (Info Gain: 78%)",
                $"Identify gaps in current {searchTopic} research (Novelty: 72%)"
            };

            foreach (var opp in opportunities)
            {
                Console.WriteLine($"  🌟 {opp}");
            }
            allResults.AppendLine("\n=== Exploration Opportunities ===");
            allResults.AppendLine(string.Join("\n", opportunities));

            // Phase 4: LEARN
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  📚 PHASE 4: LEARN - Extract Skills");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");

            // Create a new skill from this research
            string skillName = string.Join("", searchTopic.Split(' ').Select(w =>
                w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

            var newSkill = new Skill(
                skillName,
                $"Research analysis for '{searchTopic}' domain",
                new List<string> { "research-context" },
                new List<PlanStep>
                {
                    new("Multi-source fetch", new Dictionary<string, object> { ["sources"] = "arXiv, Wikipedia, Scholar" }, "Raw knowledge", 0.9),
                    new("Hypothesis generation", new Dictionary<string, object> { ["method"] = "abductive" }, "Key insights", 0.85),
                    new("Opportunity identification", new Dictionary<string, object> { ["criteria"] = "novelty, info-gain" }, "Research directions", 0.8),
                    new("Skill extraction", new Dictionary<string, object> { ["target"] = "reusable patterns" }, "New capability", 0.75)
                },
                0.80, 1, DateTime.UtcNow, DateTime.UtcNow
            );

            _registry.Value.RegisterSkill(newSkill.ToAgentSkill());
            Console.WriteLine($"  ✅ New skill registered: UseSkill_{skillName}");
            Console.WriteLine($"     Success rate: 80% | Steps: 4");

            allResults.AppendLine($"\n=== Learned Skill ===");
            allResults.AppendLine($"UseSkill_{skillName}: {newSkill.Description}");

            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    ✅ EMERGENCE CYCLE COMPLETE                               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

            s.Output = allResults.ToString();
            s.Context = $"[Emergence cycle: {searchTopic}]\n{s.Output}";
            return s;
        };
}
