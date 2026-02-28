using LangChain.DocumentLoaders;
using Ouroboros.Application.Configuration;

namespace Ouroboros.Application;

/// <summary>
/// Retrieval and LLM pipeline steps for the CLI pipeline.
/// </summary>
public static partial class CliSteps
{
    [PipelineToken("RetrieveSimilarDocuments", "RetrieveDocs", "Retrieve")]
    public static Step<CliPipelineState, CliPipelineState> RetrieveSimilarDocuments(string? args = null)
        => async s =>
        {
            int amount = s.RetrievalK;
            string? overrideQuery = null;
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("amount=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(7), out int a) && a > 0)
                        amount = a;
                    else if (part.StartsWith("query=", StringComparison.OrdinalIgnoreCase))
                        overrideQuery = part.Substring(6);
                }
            }
            string query = overrideQuery ?? (string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query);
            if (string.IsNullOrWhiteSpace(query)) return s;
            try
            {
                if (s.Branch.Store is TrackedVectorStore tvs)
                {
                    IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, query, amount);
                    s.Retrieved.Clear();
                    s.Retrieved.AddRange(hits.Select(h => h.PageContent));
                    s.Branch = s.Branch.WithIngestEvent($"retrieve:{amount}:{query.Replace('|', ':').Replace('\n', ' ')}", Enumerable.Range(0, s.Retrieved.Count).Select(i => $"doc:{i}"));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"retrieve:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("CombineDocuments", "CombineDocs")]
    public static Step<CliPipelineState, CliPipelineState> CombineDocuments(string? args = null)
        => s =>
        {
            string raw = ParseString(args);
            string separator = DefaultIngestionSettings.DocumentSeparator;
            string prefix = string.Empty;
            string suffix = string.Empty;
            int take = s.Retrieved.Count;
            bool appendToPrompt = false;
            bool clearRetrieved = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = part.Substring(4);
                        separator = value.Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("take=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int t) && t > 0)
                    {
                        take = Math.Min(t, s.Retrieved.Count);
                    }
                    else if (part.StartsWith("prefix=", StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("suffix=", StringComparison.OrdinalIgnoreCase))
                    {
                        suffix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.Equals("append", StringComparison.OrdinalIgnoreCase) || part.Equals("appendPrompt", StringComparison.OrdinalIgnoreCase))
                    {
                        appendToPrompt = true;
                    }
                    else if (part.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        clearRetrieved = true;
                    }
                }
            }

            if (take <= 0 || s.Retrieved.Count == 0)
                return Task.FromResult(s);

            List<string> blocks = s.Retrieved.Take(take).Where(static r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (blocks.Count == 0)
                return Task.FromResult(s);

            string combined = string.Join(separator, blocks);
            if (!string.IsNullOrEmpty(prefix))
                combined = prefix + combined;
            if (!string.IsNullOrEmpty(suffix))
                combined += suffix;

            s.Context = combined;
            if (appendToPrompt)
            {
                s.Prompt = string.IsNullOrWhiteSpace(s.Prompt)
                    ? combined
                    : combined + "\n\n" + s.Prompt;
            }

            if (clearRetrieved)
            {
                s.Retrieved.Clear();
            }

            return Task.FromResult(s);
        };

    [PipelineToken("Template", "UseTemplate")]
    public static Step<CliPipelineState, CliPipelineState> TemplateStep(string? args = null)
        => s =>
        {
            string templateRaw = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateRaw)) return Task.FromResult(s);
            PromptTemplate pt = new PromptTemplate(templateRaw);
            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            string formatted = pt.Format(new() { ["context"] = s.Context, ["question"] = question, ["prompt"] = s.Prompt, ["topic"] = s.Topic });
            s.Prompt = formatted; // prepared for LLM
            return Task.FromResult(s);
        };

    [PipelineToken("LLM", "RunLLM")]
    public static Step<CliPipelineState, CliPipelineState> LlmStep(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(s.Prompt)) return s;
            try
            {
                (string text, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(s.Prompt);
                s.Output = text;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(text), s.Prompt, toolCalls);
                if (s.Trace) Console.WriteLine("[trace] LLM output length=" + text.Length);
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"llm:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };
}
