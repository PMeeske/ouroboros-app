#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.RegularExpressions;
using LangChain.Databases; // for Vector, IVectorCollection
using LangChain.DocumentLoaders; // for DataSource
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Application;

/// <summary>
/// Discoverable CLI pipeline steps. Each method is annotated with PipelineToken and returns a Step over CliPipelineState.
/// Parsing of simple args is supported via optional string? args parameter.
/// </summary>
public static partial class CliSteps
{
    public static (string topic, string query) Normalize(CliPipelineState s)
    {
        string topic = string.IsNullOrWhiteSpace(s.Topic) ? (string.IsNullOrWhiteSpace(s.Prompt) ? "topic" : s.Prompt) : s.Topic;
        string query = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? topic : s.Prompt) : s.Query;
        return (topic, query);
    }

    [PipelineToken("UseAsp", "UseControllers")]
    public static Step<CliPipelineState, CliPipelineState> UseNoopAsp(string? args = null)
        => s =>
        {
            s.Branch = s.Branch.WithIngestEvent("asp:no-op", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("Set", "SetPrompt", "Step<string,string>")]
    public static Step<CliPipelineState, CliPipelineState> SetPrompt(string? args = null)
        => s =>
        {
            s.Prompt = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetTopic")]
    public static Step<CliPipelineState, CliPipelineState> SetTopic(string? args = null)
        => s =>
        {
            s.Topic = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetQuery")]
    public static Step<CliPipelineState, CliPipelineState> SetQuery(string? args = null)
        => s =>
        {
            s.Query = ParseString(args);
            return Task.FromResult(s);
        };

    // Compatibility forwards for steps defined in specialized classes
    [PipelineToken("UseDir", "DirIngest")]
    public static Step<CliPipelineState, CliPipelineState> UseDir(string? args = null)
        => IngestionCliSteps.UseDir(args);

    [PipelineToken("UseRefinementLoop")]
    public static Step<CliPipelineState, CliPipelineState> UseRefinementLoop(string? args = null)
        => ReasoningCliSteps.UseRefinementLoop(args);

    [PipelineToken("SetSource", "UseSource", "Source")]
    public static Step<CliPipelineState, CliPipelineState> SetSource(string? args = null)
        => s =>
        {
            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(s);
            // Expand ~ and relative paths
            string expanded = path.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/', '\\'))
                : path;
            string full = Path.GetFullPath(expanded);
            string finalPath = full;
            bool accessible = false;
            try
            {
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                string testFile = Path.Combine(full, ".__pipeline_access_test");
                using (File.Create(testFile)) { }
                File.Delete(testFile);
                accessible = true;
            }
            catch (OperationCanceledException) { throw; }
        catch (IOException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"source:error:{ex.GetType().Name}:{full}", Array.Empty<string>());
            }
            if (!accessible)
            {
                string fallback = Path.Combine(Environment.CurrentDirectory, "pipeline_source_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                try
                {
                    Directory.CreateDirectory(fallback);
                    finalPath = fallback;
                }
                catch (IOException ex2)
                {
                    s.Branch = s.Branch.WithIngestEvent($"source:fallback-error:{ex2.GetType().Name}:{fallback}", Array.Empty<string>());
                }
                catch (UnauthorizedAccessException ex2)
                {
                    s.Branch = s.Branch.WithIngestEvent($"source:fallback-error:{ex2.GetType().Name}:{fallback}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithSource(DataSource.FromPath(finalPath));
            s.Branch = s.Branch.WithIngestEvent($"source:set:{finalPath}", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("SetK", "UseK", "K")]
    public static Step<CliPipelineState, CliPipelineState> SetK(string? args = null)
        => s =>
        {
            if (!string.IsNullOrWhiteSpace(args))
            {
                Match m = DigitsRegex().Match(args);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int k))
                {
                    s.RetrievalK = k;
                    if (s.Trace) Console.WriteLine($"[trace] RetrievalK set to {k}");
                }
            }
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOn")]
    public static Step<CliPipelineState, CliPipelineState> TraceOn(string? args = null)
        => s =>
        {
            s.Trace = true;
            Console.WriteLine("[trace] tracing enabled");
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOff")]
    public static Step<CliPipelineState, CliPipelineState> TraceOff(string? args = null)
        => s =>
        {
            s.Trace = false;
            Console.WriteLine("[trace] tracing disabled");
            return Task.FromResult(s);
        };

    [PipelineToken("ListVectors", "Vectors")] // Optional arg 'ids' to print IDs
    public static Step<CliPipelineState, CliPipelineState> ListVectors(string? args = null)
        => s =>
        {
            IEnumerable<Vector> all = s.Branch.Store switch
            {
                Ouroboros.Domain.Vectors.TrackedVectorStore tvs => tvs.GetAll(),
                _ => Enumerable.Empty<LangChain.Databases.Vector>()
            };
            int count = all.Count();
            Console.WriteLine($"[vectors] count={count}");
            if (!string.IsNullOrWhiteSpace(args) && args.Contains("ids", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Vector? v in all.Take(100)) Console.WriteLine($" - {v.Id}");
                if (count > 100) Console.WriteLine($" ... (truncated) ...");
            }
            return Task.FromResult(s);
        };

    [PipelineToken("EmbedZip", "ZipEmbed")] // Re-embed docs that were skipped with noEmbed
    public static Step<CliPipelineState, CliPipelineState> EmbedZip(string? args = null)
        => async s =>
        {
            int batchSize = 16;
            if (!string.IsNullOrWhiteSpace(args) && args.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(args.AsSpan(6), out int bs) && bs > 0)
                batchSize = bs;
            // Heuristic: any events zip:no-embed OR zipstream:no-embed; we can't recover original text fully unless stored; for now embed placeholders.
            List<string> pendingIds = s.Branch.Events
                .Where(e => e is IngestBatch ib && (ib.Source.StartsWith("zip:no-embed") || ib.Source.StartsWith("zipstream:no-embed")))
                .SelectMany(e => ((IngestBatch)e).Ids)
                .Distinct()
                .ToList();
            if (pendingIds.Count == 0)
            {
                Console.WriteLine("[embedzip] no deferred documents found");
                return s;
            }
            Console.WriteLine($"[embedzip] embedding {pendingIds.Count} placeholder docs");
            for (int i = 0; i < pendingIds.Count; i += batchSize)
            {
                List<string> batch = pendingIds.Skip(i).Take(batchSize).ToList();
                string[] texts = batch.Select(id =>
                {
                    if (DeferredZipTextCache.TryTake(id, out string? original) && !string.IsNullOrWhiteSpace(original)) return original;
                    return $"[DEFERRED ZIP DOC] {id}";
                }).ToArray();
                try
                {
                    IReadOnlyList<float[]> emb = await s.Embed.CreateEmbeddingsAsync(texts);
                    List<Vector> vectors = new List<Vector>();
                    for (int idx = 0; idx < emb.Count; idx++)
                    {
                        string id = batch[idx];
                        vectors.Add(new Vector { Id = id, Text = texts[idx], Embedding = emb[idx] });
                    }
                    await s.Branch.Store.AddAsync(vectors);
                }
                catch (OperationCanceledException) { throw; }
        catch (IOException ex)
                {
                    foreach (string? id in batch)
                        s.Branch = s.Branch.WithIngestEvent($"zipembed:error:{id}:{ex.GetType().Name}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithIngestEvent("zipembed:complete", pendingIds);
            return s;
        };

    public static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        Match m = SingleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        m = DoubleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    // Aliases for Pipe.cs compatibility
    public static Step<CliPipelineState, CliPipelineState> LangChainRetrieveStep(string? args = null)
        => RetrieveSimilarDocuments(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainCombineStep(string? args = null)
        => CombineDocuments(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainTemplateStep(string? args = null)
        => TemplateStep(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainLlmStep(string? args = null)
        => LlmStep(args);

    [GeneratedRegex(@"\s*(\d+)\s*")]
    private static partial Regex DigitsRegex();

    [GeneratedRegex(@"^'(?<s>.*)'$", RegexOptions.Singleline)]
    private static partial Regex SingleQuotedStringRegex();

    [GeneratedRegex(@"^""(?<s>.*)""$", RegexOptions.Singleline)]
    private static partial Regex DoubleQuotedStringRegex();

    [GeneratedRegex(@"^\s*(\d+\.|\d+\)|[-*])\s*")]
    private static partial Regex ListPrefixRegex();
}
