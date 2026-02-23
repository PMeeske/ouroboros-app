using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="FetchResearchRequest"/>.
/// Fetches research papers from arXiv and optionally registers a skill.
/// </summary>
public sealed class FetchResearchHandler : IRequestHandler<FetchResearchRequest, string>
{
    private readonly OuroborosAgent _agent;

    public FetchResearchHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(FetchResearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return "Usage: fetch <research query>";

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(request.Query)}&start=0&max_results=5";
            string xml = await httpClient.GetStringAsync(url, ct);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();

            if (entries.Count == 0)
                return $"No research found for '{request.Query}'. Try a different search term.";

            // Create skill name from query
            string skillName = string.Join("", request.Query.Split(' ')
                .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

            // Register new skill if we have a skill registry
            var skills = _agent.MemorySub.Skills;
            if (skills != null)
            {
                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology from '{request.Query}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = request.Query }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "identify" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["action"] = "combine" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                skills.RegisterSkill(newSkill.ToAgentSkill());
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {entries.Count} papers on '{request.Query}':");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ");
                var summary = entry.Element(atom + "summary")?.Value?.Trim();
                var truncatedSummary = summary?.Length > 150 ? summary[..150] + "..." : summary;

                sb.AppendLine($"  \u2022 {title}");
                sb.AppendLine($"    {truncatedSummary}");
                sb.AppendLine();
            }

            if (skills != null)
                sb.AppendLine($"\u2713 New skill created: UseSkill_{skillName}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error fetching research: {ex.Message}";
        }
    }
}
