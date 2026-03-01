using System.Reactive.Linq;

namespace Ouroboros.Application;

/// <summary>
/// Partial class containing sink, output, and model-streaming pipeline steps.
/// </summary>
public static partial class StreamingCliSteps
{
    /// <summary>
    /// Outputs stream results to a sink.
    /// Sinks: 'console', 'file', 'null'
    /// Args: 'console' or 'file|path=output.txt' or 'null'
    /// </summary>
    [PipelineToken("StreamSink", "Sink")]
    public static Step<CliPipelineState, CliPipelineState> ApplySink(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string sink = options.TryGetValue("sink", out string? snk) ? snk :
                         (options.ContainsKey("console") ? "console" :
                          options.ContainsKey("file") ? "file" :
                          args?.ToLowerInvariant() ?? "console");

            IDisposable subscription;

            switch (sink.ToLowerInvariant())
            {
                case "console":
                    subscription = s.ActiveStream.Subscribe(
                        onNext: item => Console.WriteLine($"[stream] {FormatStreamItem(item)}"),
                        onError: ex => Console.WriteLine($"[stream:error] {ex.Message}"),
                        onCompleted: () => Console.WriteLine("[stream] completed"));
                    break;

                case "file":
                    string path = options.TryGetValue("path", out string? p) ? p : "stream_output.txt";
                    StreamWriter writer = new StreamWriter(path, append: true);
                    subscription = s.ActiveStream.Subscribe(
                        onNext: item => writer.WriteLine(FormatStreamItem(item)),
                        onError: ex => { writer.WriteLine($"ERROR: {ex.Message}"); writer.Flush(); },
                        onCompleted: () => { writer.WriteLine("COMPLETED"); writer.Flush(); writer.Close(); });
                    break;

                case "null":
                    subscription = s.ActiveStream.Subscribe(_ => { });
                    break;

                default:
                    subscription = s.ActiveStream.Subscribe(_ => { });
                    break;
            }

            // Register for cleanup
            s.Streaming?.Register(subscription);
            s.Branch = s.Branch.WithIngestEvent($"stream:sink:{sink}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Sink applied: {sink}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Streaming Reasoning pipeline: Thinking -> Draft -> Critique -> Improve.
    /// Uses the configured streaming model.
    /// Args: 'topic=...|k=5'
    /// </summary>
    [PipelineToken("StreamReasoning", "ReasoningStream")]
    public static Step<CliPipelineState, CliPipelineState> StreamingReasoning(string? args = null)
        => async s =>
        {
            if (s.Llm.InnerModel is not Ouroboros.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming reasoning.");
                return s;
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string topic = options.TryGetValue("topic", out string? t) ? t : "General";
            int k = options.TryGetValue("k", out string? kStr) && int.TryParse(kStr, out int kv) ? kv : 5;

            // Use query from state if not provided in args
            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                 Console.WriteLine("[error] No query provided in state.");
                 return s;
            }

            Console.WriteLine($"[stream] Starting reasoning pipeline for topic: {topic}");

            var pipeline = Ouroboros.Pipeline.Reasoning.ReasoningArrows.StreamingReasoningPipeline(
                streamingModel,
                s.Tools ?? new Ouroboros.Tools.ToolRegistry(),
                s.Embed,
                topic,
                query,
                k
            );

            try
            {
                await pipeline.ForEachAsync(item =>
                {
                    Console.Write(item.chunk);
                    s.Branch = item.branch;
                });

                Console.WriteLine(); // Newline at end
                Console.WriteLine("[stream] Reasoning pipeline completed.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[stream:error] {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Streams LLM responses directly through Rx to a console sink with real-time output.
    /// Couples IStreamingChatModel.StreamReasoningContent() -> IObservable&lt;string&gt; -> Console.Write
    /// Args: 'prefix=[stream]|newline=true'
    /// </summary>
    /// <example>
    /// Pipeline DSL: StreamToConsole('prefix=[ai]|newline=true')
    /// </example>
    [PipelineToken("StreamToConsole", "RxConsole")]
    public static Step<CliPipelineState, CliPipelineState> StreamToConsole(string? args = null)
        => async s =>
        {
            if (s.Llm.InnerModel is not Ouroboros.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming (IStreamingChatModel required).");
                return s;
            }

            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[error] No query provided. Use SetQuery('your prompt') first.");
                return s;
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string prefix = options.TryGetValue("prefix", out string? p) ? p : string.Empty;
            bool addNewline = options.TryGetValue("newline", out string? nl) &&
                              (nl.Equals("true", StringComparison.OrdinalIgnoreCase) || nl == "1");

            // Initialize streaming context for subscription management
            s.Streaming ??= new StreamingContext();

            if (!string.IsNullOrEmpty(prefix))
            {
                Console.Write($"{prefix} ");
            }

            // Create the Rx Observable from the streaming model
            IObservable<string> tokenStream = streamingModel.StreamReasoningContent(query);

            // Store active stream as IObservable<object> for composability
            s.ActiveStream = tokenStream.Select(token => (object)token);

            // Collect full response for state tracking
            System.Text.StringBuilder fullResponse = new System.Text.StringBuilder();
            var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? streamError = null;

            // Subscribe to the Rx stream with console sink
            IDisposable subscription = tokenStream.Subscribe(
                onNext: token =>
                {
                    // Real-time console output
                    Console.Write(token);
                    fullResponse.Append(token);
                },
                onError: ex =>
                {
                    Console.WriteLine();
                    Console.WriteLine($"[stream:error] {ex.Message}");
                    streamError = ex;
                    completionTcs.TrySetResult(false);
                },
                onCompleted: () =>
                {
                    if (addNewline)
                    {
                        Console.WriteLine();
                    }
                    completionTcs.TrySetResult(true);
                }
            );

            // Register subscription for cleanup
            s.Streaming.Register(subscription);

            // Wait for stream completion asynchronously via TaskCompletionSource
            await completionTcs.Task.ConfigureAwait(false);

            // Update state with the full response
            if (streamError == null)
            {
                s.Output = fullResponse.ToString();
                s.Branch = s.Branch.WithIngestEvent("stream:console:completed", new[] { $"tokens:{fullResponse.Length}" });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Stream completed: {fullResponse.Length} chars");
                }
            }

            return s;
        };

    /// <summary>
    /// Creates a live Rx Observable from model streaming that can be piped to other Rx operators.
    /// Use with ApplyMap, ApplyFilter, ApplySink for custom processing.
    /// Args: none (uses current query)
    /// </summary>
    /// <example>
    /// Pipeline DSL: CreateModelStream | ApplyFilter('length > 0') | ApplySink('console')
    /// </example>
    [PipelineToken("CreateModelStream", "ModelStream")]
    public static Step<CliPipelineState, CliPipelineState> CreateModelStream(string? args = null)
        => s =>
        {
            if (s.Llm.InnerModel is not Ouroboros.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming (IStreamingChatModel required).");
                return Task.FromResult(s);
            }

            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[error] No query provided. Use SetQuery('your prompt') first.");
                return Task.FromResult(s);
            }

            // Initialize streaming context
            s.Streaming ??= new StreamingContext();

            // Create the Rx Observable from the streaming model
            IObservable<string> tokenStream = streamingModel.StreamReasoningContent(query);

            // Store as IObservable<object> for composability with other Rx operators
            s.ActiveStream = tokenStream.Select(token => (object)token);
            s.Branch = s.Branch.WithIngestEvent("stream:model:created", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Model stream created for query: {query.Substring(0, Math.Min(50, query.Length))}...");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Streaming RAG pipeline: continuously processes queries and retrieves relevant context.
    /// Args: 'interval=5s|k=5'
    /// </summary>
    [PipelineToken("StreamRAG", "RAGStream")]
    public static Step<CliPipelineState, CliPipelineState> StreamingRag(string? args = null)
        => s =>
        {
            s.Streaming ??= new StreamingContext();

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            int intervalSeconds = options.TryGetValue("interval", out string? intv) && int.TryParse(intv.TrimEnd('s', 'S'), out int iv) ? iv : 5;
            int k = options.TryGetValue("k", out string? kStr) && int.TryParse(kStr, out int kv) ? kv : 5;

            // Create a stream that periodically queries for new prompts
            var stream = Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))
                .SelectMany(async _ =>
                {
                    if (!string.IsNullOrWhiteSpace(s.Query) && s.Branch.Store is TrackedVectorStore tvs)
                    {
                        try
                        {
                            IReadOnlyCollection<LangChain.DocumentLoaders.Document> hits = await tvs.GetSimilarDocuments(s.Embed, s.Query, k);
                            string context = string.Join("\n---\n", hits.Select(h => h.PageContent));
                            return new { Query = s.Query, Context = context, Timestamp = DateTime.UtcNow };
                        }
                        catch
                        {
                            return new { Query = s.Query, Context = string.Empty, Timestamp = DateTime.UtcNow };
                        }
                    }
                    return new { Query = string.Empty, Context = string.Empty, Timestamp = DateTime.UtcNow };
                })
                .Where(result => !string.IsNullOrWhiteSpace(result.Query));

            s.ActiveStream = stream.Select(r => (object)r);
            s.Branch = s.Branch.WithIngestEvent($"stream:rag:interval={intervalSeconds}s", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Streaming RAG created: interval={intervalSeconds}s, k={k}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Displays live metrics dashboard for the stream.
    /// Shows count, rate, recent values.
    /// Args: 'refresh=1s|items=5'
    /// </summary>
    [PipelineToken("Dashboard")]
    public static Step<CliPipelineState, CliPipelineState> ShowDashboard(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            int refreshSeconds = options.TryGetValue("refresh", out string? ref0) && int.TryParse(ref0.TrimEnd('s', 'S'), out int rs) ? rs : 1;
            int itemsToShow = options.TryGetValue("items", out string? itm) && int.TryParse(itm, out int it) ? it : 5;

            long count = 0;
            DateTime startTime = DateTime.UtcNow;
            Queue<object> recentItems = new Queue<object>();

            IDisposable subscription = s.ActiveStream.Subscribe(
                onNext: item =>
                {
                    count++;
                    recentItems.Enqueue(item);
                    if (recentItems.Count > itemsToShow)
                    {
                        recentItems.Dequeue();
                    }

                    TimeSpan elapsed = DateTime.UtcNow - startTime;
                    double rate = elapsed.TotalSeconds > 0 ? count / elapsed.TotalSeconds : 0;

                    try { Console.Clear(); } catch (IOException) { /* ignore when output is redirected */ }
                    Console.WriteLine("╔════════════════════════════════════════════════╗");
                    Console.WriteLine("║         STREAM DASHBOARD                       ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine($"║ Total Count:    {count,10}                     ║");
                    Console.WriteLine($"║ Rate:           {rate,10:F2} items/sec         ║");
                    Console.WriteLine($"║ Elapsed:        {elapsed.TotalSeconds,10:F1}s              ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine("║ Recent Items:                                  ║");

                    foreach (object recent in recentItems)
                    {
                        string display = FormatStreamItem(recent);
                        if (display.Length > 44)
                        {
                            display = display.Substring(0, 41) + "...";
                        }
                        Console.WriteLine($"║   {display,-44} ║");
                    }

                    Console.WriteLine("╚════════════════════════════════════════════════╝");
                },
                onError: ex => Console.WriteLine($"[dashboard:error] {ex.Message}"),
                onCompleted: () => Console.WriteLine("[dashboard] completed"));

            s.Streaming?.Register(subscription);
            s.Branch = s.Branch.WithIngestEvent($"stream:dashboard:refresh={refreshSeconds}s", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Dashboard created: refresh={refreshSeconds}s");
            }

            return Task.FromResult(s);
        };
}
