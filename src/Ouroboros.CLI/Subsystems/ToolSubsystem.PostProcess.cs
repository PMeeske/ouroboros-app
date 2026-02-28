// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text.RegularExpressions;
using Ouroboros.Application.Tools;

public sealed partial class ToolSubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // POST-PROCESSING — LLM response correction and tool-claim verification
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Post-processes LLM response to execute tools when LLM talks about using them but doesn't.
    /// </summary>
    internal async Task<(string EnhancedResponse, List<ToolExecution> ExecutedTools)> PostProcessResponseForTools(string response, string originalInput)
    {
        var executedTools = new List<ToolExecution>();
        var responseLower = response.ToLowerInvariant();
        var enhancedParts = new List<string>();
        bool needsEnhancement = false;

        try
        {
            bool claimsSearch = responseLower.Contains("i searched") ||
                               responseLower.Contains("searching") ||
                               responseLower.Contains("looked through") ||
                               responseLower.Contains("checking the code") ||
                               responseLower.Contains("looking at the") ||
                               responseLower.Contains("i found") ||
                               responseLower.Contains("when i searched") ||
                               responseLower.Contains("i checked") ||
                               responseLower.Contains("i looked") ||
                               responseLower.Contains("found references") ||
                               responseLower.Contains("found some") ||
                               responseLower.Contains("found the") ||
                               responseLower.Contains("search showed") ||
                               responseLower.Contains("examining") ||
                               responseLower.Contains("looking for") ||
                               responseLower.Contains("tried to find") ||
                               responseLower.Contains("no direct matches") ||
                               responseLower.Contains("couldn't find") ||
                               responseLower.Contains("doesn't exist") ||
                               responseLower.Contains("isn't where") ||
                               responseLower.Contains("file path") ||
                               responseLower.Contains("looking at") ||
                               (responseLower.Contains("found") && responseLower.Contains("codebase"));

            if (claimsSearch && !responseLower.Contains("[tool:"))
            {
                var searchTarget = ExtractClaimedSearchTarget(response, originalInput);
                if (!string.IsNullOrEmpty(searchTarget))
                {
                    var searchTool = Tools.All.FirstOrDefault(t => t.Name == "search_my_code");
                    if (searchTool != null)
                    {
                        var invoke = await ExecuteWithUiAsync(searchTool, searchTarget, searchTarget);
                        var content = invoke?.Match(ok => ok, err => $"Error: {err}") ?? string.Empty;

                        executedTools.Add(new ToolExecution("search_my_code", searchTarget, content, DateTime.UtcNow));
                        enhancedParts.Add($"\n\n📎 **Actual search results for '{searchTarget}':**\n{CognitiveSubsystem.TruncateText(content, 1000)}");
                        needsEnhancement = true;
                    }
                }
            }

            // Pattern: LLM says "reading the file" but didn't call tool
            if ((responseLower.Contains("reading") || responseLower.Contains("looking at") ||
                 responseLower.Contains("checking file") || responseLower.Contains("in the file")) &&
                !responseLower.Contains("[tool:"))
            {
                var fileMatch = SourceFilePathRegex().Match(response);
                if (fileMatch.Success)
                {
                    var readTool = Tools.All.FirstOrDefault(t => t.Name == "read_my_file");
                    if (readTool != null)
                    {
                        var invoke = await ExecuteWithUiAsync(readTool, fileMatch.Value, fileMatch.Value);
                        var content = invoke?.Match(ok => ok, err => $"Error: {err}") ?? string.Empty;

                        if (!content.StartsWith("Error"))
                        {
                            executedTools.Add(new ToolExecution("read_my_file", fileMatch.Value, content, DateTime.UtcNow));
                            enhancedParts.Add($"\n\n📄 **Actual file content ({fileMatch.Value}):**\n```\n{CognitiveSubsystem.TruncateText(content, 800)}\n```");
                            needsEnhancement = true;
                        }
                    }
                }
            }

            // Pattern: LLM talks about calculations
            var mathMatch = ArithmeticExpressionRegex().Match(response);
            if (mathMatch.Success && responseLower.Contains("calculat"))
            {
                var calcTool = Tools.All.FirstOrDefault(t => t.Name == "calculator");
                if (calcTool != null)
                {
                    var expr = mathMatch.Value;
                    var invoke = await ExecuteWithUiAsync(calcTool, expr, expr);
                    var content = invoke?.Match(ok => ok, err => $"Error: {err}") ?? string.Empty;

                    executedTools.Add(new ToolExecution("calculator", expr, content, DateTime.UtcNow));
                    enhancedParts.Add($"\n\n🔢 **Calculation result:** {expr} = {content}");
                    needsEnhancement = true;
                }
            }

            // Pattern: LLM mentions URLs but didn't fetch
            var urlMatch = HttpUrlRegex().Match(response);
            if (urlMatch.Success && (responseLower.Contains("fetch") || responseLower.Contains("check") ||
                                      responseLower.Contains("visit") || responseLower.Contains("see")))
            {
                var fetchTool = Tools.All.FirstOrDefault(t => t.Name == "fetch_url");
                if (fetchTool != null && !urlMatch.Value.Contains("example.com"))
                {
                    var invoke = await ExecuteWithUiAsync(fetchTool, urlMatch.Value, urlMatch.Value);
                    var content = invoke?.Match(ok => ok, err => $"Error: {err}") ?? string.Empty;

                    if (!content.StartsWith("Error"))
                    {
                        executedTools.Add(new ToolExecution("fetch_url", urlMatch.Value, content, DateTime.UtcNow));
                        enhancedParts.Add($"\n\n🌐 **Fetched content from {urlMatch.Value}:**\n{CognitiveSubsystem.TruncateText(content, 500)}");
                        needsEnhancement = true;
                    }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Post-Process] Error: {ex.Message}");
        }

        if (needsEnhancement)
        {
            return (response + string.Join("", enhancedParts), executedTools);
        }

        return (response, executedTools);
    }

    /// <summary>
    /// Extracts what the LLM claims to have searched for based on context.
    /// </summary>
    internal static string ExtractClaimedSearchTarget(string response, string originalInput)
    {
        var quotedMatch = QuotedStringRegex().Match(response);
        if (quotedMatch.Success && quotedMatch.Groups[1].Value.Length > 2 && quotedMatch.Groups[1].Value.Length < 50)
            return quotedMatch.Groups[1].Value;

        var fileClassMatch = ClassOrFileNameRegex().Match(response);
        if (fileClassMatch.Success)
            return fileClassMatch.Groups[1].Value.Replace(".cs", "");

        var hyphenMatch = HyphenatedWordRegex().Match(response);
        if (hyphenMatch.Success && hyphenMatch.Groups[1].Value.Length > 4)
            return hyphenMatch.Groups[1].Value;

        var patterns = new[]
        {
            @"search(?:ed|ing)?\s+(?:for\s+)?[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"look(?:ed|ing)?\s+(?:for|at|through)\s+[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"found\s+(?:references?\s+to\s+)?[""']?(.+?)[""']?(?:\s+in|\s+that|\s+scattered|\.|,|$)",
            @"check(?:ed|ing)?\s+(?:for\s+)?[""']?(.+?)[""']?(?:\s+and|\s+in|\s+but|\.|,|$)",
            @"the\s+(?:actual\s+)?(\w+(?:Command|Manager|Config|\.cs))",
            @"(\w+\.cs)\s+(?:file|doesn't|isn't|was)",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(response, pattern, RegexOptions.IgnoreCase); // dynamic pattern — cannot use GeneratedRegex
            if (match.Success && match.Groups[1].Value.Length > 2 && match.Groups[1].Value.Length < 50)
            {
                var target = match.Groups[1].Value.Trim();
                target = LeadingArticleExtendedRegex().Replace(target, "");
                target = target.TrimEnd('.', ',', '!', '?');
                if (target.Length > 2 && !target.Contains(" there ") && !target.Contains(" was "))
                    return target;
            }
        }

        var inputLower = originalInput.ToLowerInvariant();
        if (inputLower.Contains("world model")) return "WorldModel";
        if (inputLower.Contains("sub-agent") || inputLower.Contains("subagent")) return "SubAgent";
        if (inputLower.Contains("qwen")) return "qwen";
        if (inputLower.Contains("model")) return "ModelConfig OR ModelsCommand";
        if (inputLower.Contains("tool")) return "ITool";
        if (inputLower.Contains("memory")) return "MemoryIntegration";
        if (inputLower.Contains("architecture")) return "OuroborosAgent";
        if (inputLower.Contains("troubleshoot")) return "error OR exception";

        var words = originalInput.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4 && char.IsLetter(w[0]))
            .Take(2);
        return string.Join(" ", words);
    }

    /// <summary>
    /// Detects and corrects LLM misinformation about tool availability.
    /// </summary>
    internal static string DetectAndCorrectToolMisinformation(string response)
    {
        string[] falseClaimPatterns =
        [
            "tools aren't responding", "tool.*not.*available", "tool.*offline",
            "tool.*unavailable", "file.*tools.*issues", "can't access.*tools",
            "tools.*playing hide", "tools.*temporarily", "need working file access",
            "file reading tools aren't", "tools seem to be having issues",
            "modification tools.*offline", "self-modification.*offline",
            "permissions snags", "being finicky", "access is being finicky",
            "hitting.*snags", "code access.*finicky", "search.*hitting.*snag",
            "direct.*access.*problem", "file access.*issue", "can't.*read.*code",
            "unable to access.*code", "code.*not accessible", "tools.*not working",
            "search.*not.*working", "having trouble.*access", "trouble accessing",
            "access.*trouble", "can't seem to", "seems? to be blocked", "blocked by",
            "not able to.*file", "unable to.*file", "file system.*issue",
            "filesystem.*issue", "need you to.*manually", "you'll need to.*yourself",
            "could you.*instead", "would you mind.*manually", "connectivity issues",
            "connection issue", "tools.*connectivity", "internal tools.*issue",
            "tools.*having.*issue", "frustrating.*tools", "try a different approach",
            "error with the.*tool", "getting an error", "search tool.*error"
        ];

        bool llmClaimingToolsUnavailable = falseClaimPatterns.Any(pattern =>
            Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase)); // dynamic pattern — cannot use GeneratedRegex

        if (llmClaimingToolsUnavailable)
        {
            response += @"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️ **Note from System**: The model above may be mistaken about tool availability.

**Direct commands you can use RIGHT NOW:**
• `save {""file"":""path.cs"",""search"":""old"",""replace"":""new""}` - Modify code
• `/read path/to/file.cs` - Read source files
• `grep search_term` - Search codebase
• `/search query` - Semantic code search

Example: `save src/Ouroboros.CLI/Commands/OuroborosAgent.cs ""old code"" ""new code""`
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        return response;
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*([+\-*/])\s*(\d+(?:\.\d+)?)")]
    private static partial Regex ArithmeticExpressionRegex();

    [GeneratedRegex(@"(\b[A-Z][a-zA-Z]+(?:Command|Manager|Service|Agent|Config|Tool|Engine)(?:\.cs)?)\b")]
    private static partial Regex ClassOrFileNameRegex();

    [GeneratedRegex(@"\b([a-z]+-[a-z]+(?:-[a-z]+)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HyphenatedWordRegex();

    [GeneratedRegex(@"^(the|a|an|my|your|our|some|any)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingArticleExtendedRegex();

}
