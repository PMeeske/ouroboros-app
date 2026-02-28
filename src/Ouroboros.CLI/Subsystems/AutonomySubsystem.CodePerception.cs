// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.IO;
using System.Net.Http;
using System.Text;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Abstractions.Agent;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;
using Ouroboros.CLI.Subsystems.Autonomy;

/// <summary>
/// Partial: Code self-perception commands (save/read/search/analyze code) and index commands.
/// </summary>
public sealed partial class AutonomySubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CODE SELF-PERCEPTION COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct command to save/modify code using modify_my_code tool.
    /// Delegates to <see cref="SaveCodeCommandHandler"/>.
    /// </summary>
    internal async Task<string> SaveCodeCommandAsync(string argument)
    {
        _saveCodeHandler ??= new SaveCodeCommandHandler(name => Tools.Tools.GetTool(name));
        return await _saveCodeHandler.ExecuteAsync(argument);
    }

    /// <summary>
    /// Direct command to read source code using read_my_file tool.
    /// </summary>
    internal async Task<string> ReadMyCodeCommandAsync(string filePath)
    {
        try
        {
            Maybe<ITool> toolOption = Tools.Tools.GetTool("read_my_file");
            if (!toolOption.HasValue)
            {
                return "❌ Read file tool (read_my_file) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return @"📖 **Read My Code - Direct Tool Invocation**

Usage: `read my code <filepath>`

Examples:
  `read my code src/Ouroboros.CLI/Commands/OuroborosAgent.cs`
  `/read OuroborosCommands.cs`
  `cat Program.cs`";
            }

            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[ReadMyCode] Reading: {filePath}"));
            Result<string, string> result = await tool.InvokeAsync(filePath.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Failed to read file: {result.Error}";
            }
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ ReadMyCode command failed: {ex.Message}";
        }
        catch (IOException ex)
        {
            return $"❌ ReadMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to search source code using search_my_code tool.
    /// </summary>
    internal async Task<string> SearchMyCodeCommandAsync(string query)
    {
        try
        {
            Maybe<ITool> toolOption = Tools.Tools.GetTool("search_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Search code tool (search_my_code) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(query))
            {
                return @"🔍 **Search My Code - Direct Tool Invocation**

Usage: `search my code <query>`

Examples:
  `search my code tool registration`
  `/search consciousness`
  `grep modify_my_code`
  `find in code GenerateTextAsync`";
            }

            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[SearchMyCode] Searching for: {query}"));
            Result<string, string> result = await tool.InvokeAsync(query.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Search failed: {result.Error}";
            }
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ SearchMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to analyze and improve code using Roslyn tools.
    /// Bypasses LLM to use tools directly.
    /// </summary>
    internal async Task<string> AnalyzeCodeCommandAsync(string input)
    {
        StringBuilder sb = new();
        sb.AppendLine("🔍 **Code Analysis - Direct Tool Invocation**\n");

        try
        {
            // Step 1: Search for C# files to analyze
            Maybe<ITool> searchTool = Tools.Tools.GetTool("search_my_code");
            Maybe<ITool> analyzeTool = Tools.Tools.GetTool("analyze_csharp_code");
            Maybe<ITool> readTool = Tools.Tools.GetTool("read_my_file");

            if (!searchTool.HasValue)
            {
                return "❌ search_my_code tool not available.";
            }

            // Find some key C# files
            sb.AppendLine("**Scanning codebase for C# files...**\n");
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[AnalyzeCode] Searching for key files..."));

            string[] searchTerms = new[] { "OuroborosAgent", "ChatAsync", "ITool", "ToolRegistry" };
            List<string> foundFiles = new();

            foreach (string term in searchTerms)
            {
                Result<string, string> searchResult = await searchTool.GetValueOrDefault(null!)!.InvokeAsync(term);
                if (searchResult.IsSuccess)
                {
                    // Extract file paths from search results
                    foreach (string line in searchResult.Value.Split('\n'))
                    {
                        if (line.Contains(".cs") && line.Contains("src/"))
                        {
                            // Extract the file path
                            int start = line.IndexOf("src/");
                            if (start >= 0)
                            {
                                int end = line.IndexOf(".cs", start) + 3;
                                if (end > start)
                                {
                                    string filePath = line[start..end];
                                    if (!foundFiles.Contains(filePath))
                                    {
                                        foundFiles.Add(filePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (foundFiles.Count == 0)
            {
                foundFiles.Add("src/Ouroboros.CLI/Commands/OuroborosAgent.cs");
                foundFiles.Add("src/Ouroboros.Application/Tools/SystemAccessTools.cs");
            }

            sb.AppendLine($"Found {foundFiles.Count} files to analyze:\n");
            foreach (string file in foundFiles.Take(5))
            {
                sb.AppendLine($"  • {file}");
            }
            sb.AppendLine();

            // Step 2: If Roslyn analyzer is available, use it
            if (analyzeTool.HasValue)
            {
                sb.AppendLine("**Running Roslyn analysis...**\n");
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("[AnalyzeCode] Running Roslyn analysis..."));

                string sampleFile = foundFiles.FirstOrDefault() ?? "src/Ouroboros.CLI/Commands/OuroborosAgent.cs";
                if (readTool.HasValue)
                {
                    Result<string, string> readResult = await readTool.GetValueOrDefault(null!)!.InvokeAsync(sampleFile);
                    if (readResult.IsSuccess && readResult.Value.Length < 50000)
                    {
                        // Analyze a portion of the code
                        string codeSnippet = readResult.Value.Length > 5000
                            ? readResult.Value[..5000]
                            : readResult.Value;

                        Result<string, string> analyzeResult = await analyzeTool.GetValueOrDefault(null!)!.InvokeAsync(codeSnippet);
                        if (analyzeResult.IsSuccess)
                        {
                            sb.AppendLine("**Analysis Results:**\n");
                            sb.AppendLine(analyzeResult.Value);
                        }
                    }
                }
            }

            // Step 3: Provide actionable commands
            sb.AppendLine("\n**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");
            sb.AppendLine("**Direct commands to modify code:**\n");
            sb.AppendLine("```");
            sb.AppendLine($"/read {foundFiles.FirstOrDefault()}");
            sb.AppendLine($"grep <search_term>");
            sb.AppendLine($"save {{\"file\":\"{foundFiles.FirstOrDefault()}\",\"search\":\"old text\",\"replace\":\"new text\"}}");
            sb.AppendLine("```\n");
            sb.AppendLine("To make a specific change, use:");
            sb.AppendLine("  1. `/read <file>` to see current content");
            sb.AppendLine("  2. `save {\"file\":\"...\",\"search\":\"...\",\"replace\":\"...\"}` to modify");
            sb.AppendLine("**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");

            return sb.ToString();
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Code analysis failed: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INDEX COMMANDS (migrated from OuroborosAgent)
    // ═══════════════════════════════════════════════════════════════════════════

    internal async Task<string> ReindexFullAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape("[~] Starting full workspace reindex...")}[/]");

            var result = await SelfIndexer.FullReindexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Full Reindex Complete**\n");
            sb.AppendLine($"  • Processed files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Skipped files: {result.SkippedFiles}");
            sb.AppendLine($"  • Errors: {result.ErrorFiles}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");
            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"❌ Reindex failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Reindex failed: {ex.Message}";
        }
    }

    internal async Task<string> ReindexIncrementalAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]{Markup.Escape("[~] Starting incremental reindex (changed files only)...")}[/]");

            var result = await SelfIndexer.IncrementalIndexAsync();

            var sb = new StringBuilder();
            sb.AppendLine("✅ **Incremental Reindex Complete**\n");
            sb.AppendLine($"  • Updated files: {result.ProcessedFiles}");
            sb.AppendLine($"  • Indexed chunks: {result.IndexedChunks}");
            sb.AppendLine($"  • Duration: {result.Elapsed.TotalSeconds:F1}s");
            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"❌ Incremental reindex failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Incremental reindex failed: {ex.Message}";
        }
    }

    internal async Task<string> IndexSearchAsync(string query)
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        if (string.IsNullOrWhiteSpace(query))
        {
            return @"🔍 **Index Search - Semantic Code Search**

Usage: `index search <query>`

Examples:
  `index search how is TTS initialized`
  `index search error handling patterns`
  `index search tool registration`";
        }

        try
        {
            var results = await SelfIndexer.SearchAsync(query, limit: 5);

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 **Index Search Results for:** \"{query}\"\n");

            if (results.Count == 0)
            {
                sb.AppendLine("No results found. Try running `reindex` to update the index.");
            }
            else
            {
                foreach (var result in results)
                {
                    sb.AppendLine($"**{result.FilePath}** (score: {result.Score:F2})");
                    sb.AppendLine($"```");
                    sb.AppendLine(result.Content.Length > 500 ? result.Content[..500] + "..." : result.Content);
                    sb.AppendLine($"```\n");
                }
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"❌ Index search failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Index search failed: {ex.Message}";
        }
    }

    internal async Task<string> GetIndexStatsAsync()
    {
        if (SelfIndexer == null)
            return "❌ Self-indexer not available. Qdrant may not be running.";

        try
        {
            var stats = await SelfIndexer.GetStatsAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📊 **Code Index Statistics**\n");
            sb.AppendLine($"  • Collection: {stats.CollectionName}");
            sb.AppendLine($"  • Total vectors: {stats.TotalVectors}");
            sb.AppendLine($"  • Indexed files: {stats.IndexedFiles}");
            sb.AppendLine($"  • Vector size: {stats.VectorSize}");
            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"❌ Failed to get index stats: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"❌ Failed to get index stats: {ex.Message}";
        }
    }
}
