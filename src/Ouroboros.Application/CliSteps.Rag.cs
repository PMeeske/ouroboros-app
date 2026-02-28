using System.Text;
using System.Text.RegularExpressions;
using LangChain.DocumentLoaders;
using Ouroboros.Application.Configuration;

namespace Ouroboros.Application;

/// <summary>
/// RAG (Retrieval-Augmented Generation) pipeline steps: Divide-and-Conquer, Decompose-and-Aggregate,
/// guided installation, and dependency error handling.
/// </summary>
public static partial class CliSteps
{
    /// <summary>
    /// Divide-and-Conquer RAG: retrieve K docs, split into groups, answer per group, then synthesize final.
    /// Args: 'k=24|group=6|template=...|final=...|sep=\\n---\\n'
    /// If s.Retrieved is empty, it will retrieve using current Query/Prompt.
    /// </summary>
    [PipelineToken("DivideAndConquerRAG", "DCRAG", "RAGMapReduce")]
    public static Step<CliPipelineState, CliPipelineState> DivideAndConquerRag(string? args = null)
        => async s =>
        {
            int k = Math.Max(4, s.RetrievalK);
            int group = RagDefaults.GroupSize;
            string sep = DefaultIngestionSettings.DocumentSeparator;
            string? template = null;
            string? finalTemplate = null;
            bool streamPartials = false; // print intermediate outputs to console

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("group=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(6), out int gv) && gv > 0) group = gv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) template = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTemplate = part.Substring(6);
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) streamPartials = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        streamPartials = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Ensure retrieved context
            if (s.Retrieved.Count == 0)
            {
                try { s = await RetrieveSimilarDocuments($"amount={k}")(s); } catch (HttpRequestException) { /* retrieval optional — ignore */ }
            }
            if (s.Retrieved.Count == 0) return s;

            // Defaults
            template ??= "Use the following context to answer the question. Be precise and concise.\n{context}\n\nQuestion: {question}\nAnswer:";
            finalTemplate ??= "You are to synthesize a final, precise answer from multiple partial answers.\nQuestion: {question}\n\nPartial Answers:\n{partials}\n\nFinal Answer:";

            // Partition into groups
            List<string> docs = s.Retrieved.Where(static r => !string.IsNullOrWhiteSpace(r)).Take(k).ToList();
            if (docs.Count == 0) return s;

            List<List<string>> groups = new List<List<string>>();
            for (int i = 0; i < docs.Count; i += group)
            {
                groups.Add(docs.Skip(i).Take(group).ToList());
            }

            List<string> partials = new List<string>(groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<string> g = groups[gi];
                string ctx = string.Join(sep, g);
                string prompt = template!
                    .Replace("{context}", ctx)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string answer, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
                    partials.Add(answer ?? string.Empty);
                    // Record as reasoning step for traceability
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer ?? string.Empty), prompt, toolCalls);
                    if (streamPartials || s.Trace)
                    {
                        Console.WriteLine($"\n>> [dcrag] partial {gi + 1}/{groups.Count} (docs={g.Count})");
                        if (!string.IsNullOrWhiteSpace(answer))
                        {
                            Console.WriteLine(answer);
                        }
                        Console.Out.Flush();
                    }
                }
                catch (HttpRequestException ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"dcrag:part-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // Synthesize final
            string partialText = string.Join("\n\n---\n\n", partials.Where(p => !string.IsNullOrWhiteSpace(p)));
            string finalPrompt = finalTemplate!
                .Replace("{partials}", partialText)
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalToolCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalToolCalls);
                if (s.Trace) Console.WriteLine($"[trace] DCRAG final length={s.Output.Length}");
                if (streamPartials)
                {
                    Console.WriteLine("\n=== DCRAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (HttpRequestException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"dcrag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    /// <summary>
    /// Decompose-and-Aggregate RAG: decomposes the main question into sub-questions, answers each with retrieved context,
    /// then synthesizes a final unified answer. Supports streaming of sub-answers and final aggregation.
    /// Args: 'subs=4|per=6|k=24|sep=\n---\n|stream|decompose=...|template=...|final=...'
    /// - subs: number of subquestions to generate (default 4)
    /// - per: number of retrieved docs per subquestion (default 6)
    /// - k: optional initial retrieval to warm cache or pre-fill (ignored if not needed)
    /// - sep: separator for combining docs
    /// - stream: print each sub-answer and the final result
    /// - decompose: custom prompt template for subquestion generation; placeholders: {question}
    /// - template: custom prompt for answering subquestions; placeholders: {context}, {subquestion}, {question}, {prompt}, {topic}
    /// - final: custom prompt for the final synthesis; placeholders: {pairs}, {question}, {prompt}, {topic}
    /// </summary>
    [PipelineToken("DecomposeAndAggregateRAG", "DARAG", "SubQAggregate")]
    public static Step<CliPipelineState, CliPipelineState> DecomposeAndAggregateRag(string? args = null)
        => async s =>
        {
            // Defaults and args
            int subs = RagDefaults.SubQuestions;
            int per = RagDefaults.DocumentsPerSubQuestion;
            int k = Math.Max(4, s.RetrievalK);
            string sep = DefaultIngestionSettings.DocumentSeparator;
            bool stream = false;
            string? decomposeTpl = null;
            string? subTpl = null;
            string? finalTpl = null;

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("subs=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int sv) && sv > 0) subs = sv;
                    else if (part.StartsWith("per=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(4), out int pv) && pv > 0) per = pv;
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) stream = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        stream = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (part.StartsWith("decompose=", StringComparison.OrdinalIgnoreCase)) decomposeTpl = part.Substring(10);
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) subTpl = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTpl = part.Substring(6);
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Optional: warm retrieval cache using the main question
            try { s = await RetrieveSimilarDocuments($"amount={k}|query={question.Replace("|", ":")}")(s); } catch (HttpRequestException) { /* retrieval cache warm-up optional — ignore */ }

            // Default templates
            decomposeTpl ??= "You are tasked with answering a complex question by breaking it down into distinct sub-questions that together fully address the original.\n" +
                             "Main question: {question}\n\n" +
                             "Return exactly {N} non-overlapping sub-questions as a numbered list (1., 2., ...), one per line, focused and specific.";

            subTpl ??= "You are answering a sub-question as part of a larger task.\n" +
                      "Main question: {question}\nSub-question: {subquestion}\n\n" +
                      "Use the following context snippets to produce a precise, thorough answer. Cite facts from context; avoid speculation.\n" +
                      "Context:\n{context}\n\n" +
                      "Answer:";

            finalTpl ??= "Synthesize a high-quality final answer to the main question by integrating the following detailed sub-answers.\n" +
                       "Provide:\n- Executive summary (3-6 bullets)\n- Integrated comprehensive answer tying together all parts\n- If relevant: Considerations and Next steps\n\n" +
                       "Main question: {question}\n\nSub-answers:\n{pairs}\n\nFinal Answer:";

            // 1) Generate sub-questions
            string decomposePrompt = decomposeTpl
                .Replace("{question}", question)
                .Replace("{N}", subs.ToString());

            List<string> subQuestions = new();
            try
            {
                (string subText, List<ToolExecution> subCalls) = await s.Llm.GenerateWithToolsAsync(decomposePrompt);
                s.Branch = s.Branch.WithReasoning(new FinalSpec(subText ?? string.Empty), decomposePrompt, subCalls);
                if (!string.IsNullOrWhiteSpace(subText))
                {
                    foreach (string line in subText.Split('\n'))
                    {
                        string t = line.Trim();
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        // Accept formats like: "1. ...", "1) ...", "- ..." or plain line
                        t = ListPrefixRegex().Replace(t, string.Empty);
                        if (!string.IsNullOrWhiteSpace(t)) subQuestions.Add(t);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:decompose-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            if (subQuestions.Count == 0)
            {
                // Fallback: use the original question as a single sub-question
                subQuestions.Add(question);
            }
            else if (subQuestions.Count > subs)
            {
                subQuestions = subQuestions.Take(subs).ToList();
            }

            // 2) Answer each sub-question with retrieval
            List<(string q, string a)> qaPairs = new List<(string q, string a)>(subQuestions.Count);
            for (int i = 0; i < subQuestions.Count; i++)
            {
                string sq = subQuestions[i];
                // Retrieve per sub-question
                List<string> blocks = new();
                try
                {
                    if (s.Branch.Store is TrackedVectorStore tvs)
                    {
                        IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, sq, per);
                        foreach (Document doc in hits)
                        {
                            if (!string.IsNullOrWhiteSpace(doc.PageContent))
                                blocks.Add(doc.PageContent);
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:retrieve-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }

                string ctx = string.Join(sep, blocks);
                string subPrompt = subTpl
                    .Replace("{context}", ctx)
                    .Replace("{subquestion}", sq)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string ans, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(subPrompt);
                    string answer = ans ?? string.Empty;
                    qaPairs.Add((sq, answer));
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer), subPrompt, toolCalls);
                    if (stream || s.Trace)
                    {
                        Console.WriteLine($"\n>> [darag] sub {i + 1}/{subQuestions.Count}: {sq}");
                        if (!string.IsNullOrWhiteSpace(answer)) Console.WriteLine(answer);
                        Console.Out.Flush();
                    }
                }
                catch (HttpRequestException ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:sub-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // 3) Final synthesis
            StringBuilder sbPairs = new StringBuilder();
            for (int i = 0; i < qaPairs.Count; i++)
            {
                (string q, string a) = qaPairs[i];
                sbPairs.AppendLine($"Sub-question {i + 1}: {q}");
                sbPairs.AppendLine("Answer:");
                sbPairs.AppendLine(a);
                sbPairs.AppendLine();
            }

            string finalPrompt = finalTpl
                .Replace("{pairs}", sbPairs.ToString())
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalCalls);
                if (s.Trace) Console.WriteLine($"[trace] DARAG final length={s.Output.Length}");
                if (stream)
                {
                    Console.WriteLine("\n=== DARAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (HttpRequestException ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };

    [PipelineToken("InstallDependenciesGuided", "GuidedInstall")]
    public static Step<CliPipelineState, CliPipelineState> InstallDependenciesGuided(string? args = null)
        => async s =>
        {
            string raw = ParseString(args);
            string? dep = null;
            string? error = null;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("dep=", StringComparison.OrdinalIgnoreCase)) dep = part.Substring(4);
                    else if (part.StartsWith("error=", StringComparison.OrdinalIgnoreCase)) error = part.Substring(6);
                }
            }

            string eventSource = "guided-install:triggered";
            if (!string.IsNullOrEmpty(dep)) eventSource += $":{dep}";
            if (!string.IsNullOrEmpty(error)) eventSource += $":{error}";

            s.Branch = s.Branch.WithIngestEvent(eventSource, Array.Empty<string>());

            // If we have a specific dependency, we might want to schedule a fix or prompt the user
            if (!string.IsNullOrEmpty(dep))
            {
                if (s.Trace) Console.WriteLine($"[guided-install] Dependency missing: {dep}");

                // In a real scenario, this might trigger an interactive prompt or a specific agent flow
            }

            return await Task.FromResult(s);
        };

    private static async Task<CliPipelineState> HandleDependencyExceptionAsync(CliPipelineState s, Exception ex)
    {
        string msg = ex.Message;
        string? dep = null;

        if (msg.Contains("NuGet", StringComparison.OrdinalIgnoreCase)) dep = "NuGet";
        else if (msg.Contains("npm", StringComparison.OrdinalIgnoreCase)) dep = "NPM";
        else if (msg.Contains("ollama", StringComparison.OrdinalIgnoreCase)) dep = "Ollama";
        else if (msg.Contains("pip", StringComparison.OrdinalIgnoreCase)) dep = "Python";
        else if (msg.Contains("docker", StringComparison.OrdinalIgnoreCase)) dep = "Docker";

        if (dep != null)
        {
            s.Branch = s.Branch.WithIngestEvent($"dependency:missing:{dep}", Array.Empty<string>());
            s.Branch = s.Branch.WithIngestEvent("schedule:guided-install", Array.Empty<string>());
            if (s.Trace) Console.WriteLine($"[dependency-handler] Detected missing dependency: {dep}");
        }
        else
        {
            s.Branch = s.Branch.WithIngestEvent($"error:generic:{ex.GetType().Name}", Array.Empty<string>());
        }

        return await Task.FromResult(s);
    }
}
