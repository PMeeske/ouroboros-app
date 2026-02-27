#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill CLI Steps - Skill Management (Init, UseSkill, Suggest, Fetch, List, Execute)
// ==========================================================

using System.Text;
using System.Xml.Linq;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    /// <summary>
    /// Initialize and list available skills.
    /// Usage: SkillInit | ... | UseOutput
    /// </summary>
    [PipelineToken("SkillInit", "InitSkills", "LoadSkills")]
    public static Step<CliPipelineState, CliPipelineState> SkillInit(string? args = null)
        => s =>
        {
            var skills = _registry.Value.GetAllSkills();
            Console.WriteLine($"[SkillInit] Loaded {skills.Count} skills:");
            foreach (var skill in skills.Take(5))
            {
                Console.WriteLine($"  • {skill.Name} ({skill.SuccessRate:P0})");
            }
            if (skills.Count > 5)
                Console.WriteLine($"  ... and {skills.Count - 5} more");

            s.Context = string.Join("\n", skills.Select(sk => $"- {sk.Name}: {sk.Description}"));
            return Task.FromResult(s);
        };

    /// <summary>
    /// Apply literature review skill - synthesizes research into coherent review.
    /// Usage: SetPrompt 'AI safety research' | UseSkill_LiteratureReview | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_LiteratureReview", "LitReview", "ReviewLiterature")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillLiteratureReview(string? args = null)
        => ExecuteSkill("LiteratureReview", args);

    /// <summary>
    /// Apply hypothesis generation skill - generates testable hypotheses.
    /// Usage: SetPrompt 'observations about X' | UseSkill_HypothesisGeneration | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_HypothesisGeneration", "GenHypothesis", "Hypothesize")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillHypothesisGeneration(string? args = null)
        => ExecuteSkill("HypothesisGeneration", args);

    /// <summary>
    /// Apply chain-of-thought reasoning skill - step-by-step problem solving.
    /// Usage: SetPrompt 'complex problem' | UseSkill_ChainOfThought | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_ChainOfThought", "UseSkill_ChainOfThoughtReasoning", "ChainOfThought", "CoT")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillChainOfThought(string? args = null)
        => ExecuteSkill("ChainOfThoughtReasoning", args);

    /// <summary>
    /// Apply cross-domain transfer skill - transfer insights between domains.
    /// Usage: SetPrompt 'apply biology patterns to software' | UseSkill_CrossDomain | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_CrossDomain", "UseSkill_CrossDomainTransfer", "CrossDomain", "TransferInsight")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillCrossDomain(string? args = null)
        => ExecuteSkill("CrossDomainTransfer", args);

    /// <summary>
    /// Apply citation analysis skill - analyze citation networks.
    /// Usage: SetPrompt 'analyze citations in ML papers' | UseSkill_CitationAnalysis | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_CitationAnalysis", "CitationAnalysis", "AnalyzeCitations")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillCitationAnalysis(string? args = null)
        => ExecuteSkill("CitationAnalysis", args);

    /// <summary>
    /// Apply emergent discovery skill - find emergent patterns.
    /// Usage: SetPrompt 'find patterns in data' | UseSkill_EmergentDiscovery | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_EmergentDiscovery", "EmergentDiscovery", "DiscoverPatterns")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillEmergentDiscovery(string? args = null)
        => ExecuteSkill("EmergentDiscovery", args);

    /// <summary>
    /// Dynamic skill execution by name.
    /// Usage: UseSkill 'SkillName' | UseOutput
    /// </summary>
    [PipelineToken("UseSkill", "ApplySkill", "RunSkill")]
    public static Step<CliPipelineState, CliPipelineState> UseSkill(string? skillName = null)
        => ExecuteSkill(ParseString(skillName) is { Length: > 0 } parsed ? parsed : "ChainOfThoughtReasoning", null);

    /// <summary>
    /// Suggest skills based on current prompt/context.
    /// Usage: SetPrompt 'reasoning task' | SuggestSkill | UseOutput
    /// </summary>
    [PipelineToken("SuggestSkill", "SkillSuggest", "FindSkill")]
    public static Step<CliPipelineState, CliPipelineState> SuggestSkill(string? args = null)
        => async s =>
        {
            string query = ParseString(args) is { Length: > 0 } parsed ? parsed : (s.Prompt ?? s.Query);
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[SuggestSkill] No query provided, use SetPrompt first");
                return s;
            }

            var matches = await _registry.Value.FindMatchingSkillsAsync(query);
            if (matches.Count == 0)
            {
                Console.WriteLine("[SuggestSkill] No matching skills found");
                s.Output = "No matching skills found for the given query.";
                return s;
            }

            Console.WriteLine($"[SuggestSkill] Found {matches.Count} matching skills:");
            var suggestions = new List<string>();
            foreach (var skill in matches.Take(3))
            {
                Console.WriteLine($"  🎯 UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
                Console.WriteLine($"     {skill.Description}");
                suggestions.Add($"UseSkill_{skill.Name}");
            }

            s.Output = $"Suggested skills: {string.Join(", ", suggestions)}";
            s.Context = string.Join("\n", matches.Take(3).Select(sk => $"{sk.Name}: {sk.Description}"));
            return s;
        };

    /// <summary>
    /// Fetch research and extract new skills from arXiv.
    /// Usage: FetchSkill 'chain of thought' | UseOutput
    /// </summary>
    [PipelineToken("FetchSkill", "LearnSkill", "ResearchSkill")]
    public static Step<CliPipelineState, CliPipelineState> FetchSkill(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) is { Length: > 0 } parsed ? parsed : (s.Prompt ?? s.Query);
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[FetchSkill] No query provided");
                return s;
            }

            Console.WriteLine($"[FetchSkill] Fetching research on: \"{searchQuery}\"...");

            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(searchQuery)}&start=0&max_results=5";

            try
            {
                string xml = await httpClient.GetStringAsync(url);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
                var entries = doc.Descendants(atom + "entry").Take(5).ToList();

                Console.WriteLine($"[FetchSkill] Found {entries.Count} papers");

                // Extract skill from query pattern
                string skillName = string.Join("", searchQuery.Split(' ').Select(w =>
                    w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology derived from '{searchQuery}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = searchQuery }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "analysis" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["output"] = "knowledge" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow
                );
                _registry.Value.RegisterSkill(newSkill.ToAgentSkill());

                Console.WriteLine($"[FetchSkill] ✅ New skill registered: UseSkill_{skillName}");
                s.Output = $"Learned new skill: UseSkill_{skillName} from {entries.Count} papers";
                s.Context = string.Join("\n", entries.Select(e =>
                    e.Element(atom + "title")?.Value?.Trim() ?? "Untitled"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FetchSkill] ⚠ Failed: {ex.Message}");
                s.Output = $"Failed to fetch research: {ex.Message}";
            }

            return s;
        };

    /// <summary>
    /// List all available skill tokens.
    /// Usage: ListSkills | UseOutput
    /// </summary>
    [PipelineToken("ListSkills", "SkillList", "ShowSkills")]
    public static Step<CliPipelineState, CliPipelineState> ListSkills(string? args = null)
        => s =>
        {
            var skills = _registry.Value.GetAllSkills();
            Console.WriteLine($"[ListSkills] {skills.Count} registered skills:");

            var output = new List<string>();
            foreach (var skill in skills)
            {
                string line = $"UseSkill_{skill.Name} ({skill.SuccessRate:P0}) - {skill.Description}";
                Console.WriteLine($"  • {line}");
                output.Add(line);
            }

            s.Output = string.Join("\n", output);
            return Task.FromResult(s);
        };

    /// <summary>
    /// Print the current output to console.
    /// Usage: ... | UseSkill_X | PrintOutput
    /// </summary>
    [PipelineToken("PrintOutput", "ShowOutput", "DisplayResult")]
    public static Step<CliPipelineState, CliPipelineState> PrintOutput(string? args = null)
        => s =>
        {
            if (!string.IsNullOrWhiteSpace(s.Output))
            {
                Console.WriteLine("\n=== SKILL OUTPUT ===");
                Console.WriteLine(s.Output);
                Console.WriteLine("====================\n");
            }
            else
            {
                Console.WriteLine("[PrintOutput] No output available");
            }
            return Task.FromResult(s);
        };

    // === Skill Execution Engine ===

    private static Step<CliPipelineState, CliPipelineState> ExecuteSkill(string skillName, string? args)
        => async s =>
        {
            var skill = _registry.Value.GetAllSkills()
                .FirstOrDefault(sk => sk.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Console.WriteLine($"[UseSkill] ⚠ Skill '{skillName}' not found");
                s.Output = $"Skill '{skillName}' not found. Use ListSkills to see available skills.";
                return s;
            }

            string input = args ?? s.Prompt ?? s.Query ?? s.Context;
            var skillData = skill.ToSkill();
            Console.WriteLine($"[UseSkill_{skill.Name}] Executing with {skillData.Steps.Count} steps...");

            // Build a prompt that applies the skill's methodology
            var stepDescriptions = skillData.Steps.Select(step => $"- {step.Action}: {step.ExpectedOutcome}");
            string methodology = string.Join("\n", stepDescriptions);

            string skillPrompt = $"""
                Apply the "{skill.Name}" methodology to the following input.

                Methodology steps:
                {methodology}

                Input: {input}

                Execute each step systematically and provide the final result.
                """;

            try
            {
                // Execute through the LLM
                string result = await s.Llm.InnerModel.GenerateTextAsync(skillPrompt);

                // Record skill execution for learning
                _registry.Value.RecordSkillExecution(skill.Name, !string.IsNullOrWhiteSpace(result), 0L);

                if (string.IsNullOrWhiteSpace(result))
                {
                    Console.WriteLine($"[UseSkill_{skill.Name}] ⚠ LLM returned empty response (is Ollama running?)");

                    // Provide simulated output for demo purposes
                    result = $"""
                        [Simulated {skill.Name} Analysis]

                        Applied methodology to: {input}

                        Steps executed:
                        {methodology}

                        Note: LLM unavailable - this is a placeholder response.
                        Start Ollama with 'ollama serve' for full functionality.
                        """;
                }

                Console.WriteLine($"[UseSkill_{skill.Name}] ✓ Complete");
                Console.WriteLine($"\n--- {skill.Name} Result ---");
                Console.WriteLine(result.Length > 500 ? result[..500] + "..." : result);
                Console.WriteLine("----------------------------\n");

                s.Output = result;
                s.Context = $"[{skill.Name}] {result}";
            }
            catch (Exception ex)
            {
                _registry.Value.RecordSkillExecution(skill.Name, false, 0L);
                Console.WriteLine($"[UseSkill_{skill.Name}] ⚠ Failed: {ex.Message}");
                s.Output = $"Skill execution failed: {ex.Message}";
            }

            return s;
        };

    private static void RegisterPredefinedSkills(SkillRegistry registry)
    {
        static PlanStep MakeStep(string action, string param, string outcome, double confidence) =>
            new(action, new Dictionary<string, object> { ["hint"] = param }, outcome, confidence);

        var predefinedSkills = new[]
        {
            new Skill("LiteratureReview", "Synthesize research papers into coherent review",
                new List<string> { "research-context" },
                new List<PlanStep> { MakeStep("Identify themes", "Scan papers", "Key themes", 0.9),
                        MakeStep("Compare findings", "Cross-reference", "Patterns", 0.85),
                        MakeStep("Synthesize", "Combine insights", "Review", 0.8) },
                0.85, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("HypothesisGeneration", "Generate testable hypotheses from observations",
                new List<string> { "observations" },
                new List<PlanStep> { MakeStep("Find gaps", "Identify unknowns", "Questions", 0.8),
                        MakeStep("Generate hypotheses", "Form predictions", "Hypotheses", 0.75),
                        MakeStep("Rank by testability", "Evaluate", "Ranked list", 0.7) },
                0.78, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("ChainOfThoughtReasoning", "Apply step-by-step reasoning to problems",
                new List<string> { "problem-statement" },
                new List<PlanStep> { MakeStep("Decompose problem", "Break down", "Sub-problems", 0.9),
                        MakeStep("Reason through steps", "Apply logic", "Intermediate results", 0.85),
                        MakeStep("Synthesize answer", "Combine", "Final answer", 0.88) },
                0.88, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CrossDomainTransfer", "Transfer insights across domains",
                new List<string> { "source-domain", "target-domain" },
                new List<PlanStep> { MakeStep("Abstract patterns", "Generalize", "Abstract concepts", 0.7),
                        MakeStep("Map to target", "Apply", "Mapped insights", 0.65),
                        MakeStep("Validate transfer", "Test", "Validated insights", 0.6) },
                0.65, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CitationAnalysis", "Analyze citation networks for influence",
                new List<string> { "paper-set" },
                new List<PlanStep> { MakeStep("Build citation graph", "Extract refs", "Graph", 0.85),
                        MakeStep("Rank by influence", "PageRank-style", "Rankings", 0.8),
                        MakeStep("Find key papers", "Identify", "Key papers", 0.82) },
                0.82, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("EmergentDiscovery", "Discover emergent patterns from multiple sources",
                new List<string> { "multiple-sources" },
                new List<PlanStep> { MakeStep("Combine sources", "Merge data", "Combined view", 0.75),
                        MakeStep("Find emergent patterns", "Pattern detection", "Patterns", 0.7),
                        MakeStep("Validate discoveries", "Cross-check", "Validated discoveries", 0.65) },
                0.71, 0, DateTime.UtcNow, DateTime.UtcNow),
        };

        foreach (var skill in predefinedSkills)
            registry.RegisterSkill(skill.ToAgentSkill());
    }
}
