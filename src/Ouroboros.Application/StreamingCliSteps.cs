#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Linq;
using System.Threading.Channels;

namespace Ouroboros.Application;

/// <summary>
/// Streaming CLI pipeline steps using System.Reactive for live stream processing.
/// Provides operators for creating, transforming, aggregating, and outputting streams.
/// </summary>
/// <remarks>
/// This class is split into partial files:
/// - StreamingCliSteps.cs (this file): Core stream creation, windowing, aggregation, map, and filter steps
/// - StreamingCliSteps.Sinks.cs: Sink, output, model streaming, reasoning, RAG, and dashboard steps
/// - StreamingCliSteps.Helpers.cs: Stream creation helpers, aggregate operators, and utility methods
/// </remarks>
public static partial class StreamingCliSteps
{
    /// <summary>
    /// Creates a stream from a specified source.
    /// Sources: 'generated' (generates test data), 'file' (reads from file), 'channel' (from channel)
    /// Args: 'source=generated|count=100|interval=100' or 'source=file|path=data.txt'
    /// </summary>
    [PipelineToken("Stream", "UseStream")]
    public static Step<CliPipelineState, CliPipelineState> CreateStream(string? args = null)
        => s =>
        {
            // Ensure streaming context exists
            s.Streaming ??= new StreamingContext();

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string source = options.TryGetValue("source", out string? src) ? src : "generated";

            IObservable<object> stream = source.ToLowerInvariant() switch
            {
                "generated" => CreateGeneratedStream(options, s),
                "file" => CreateFileStream(options, s),
                "channel" => CreateChannelStream(options, s),
                _ => Observable.Empty<object>()
            };

            s.ActiveStream = stream;
            s.Branch = s.Branch.WithIngestEvent($"stream:created:{source}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Stream created: {source}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies windowing to the active stream.
    /// Supports tumbling and sliding windows.
    /// Args: 'size=5s' or 'size=10|slide=5' for count-based, 'size=5s|slide=2s' for time-based
    /// </summary>
    [PipelineToken("StreamWindow", "Window")]
    public static Step<CliPipelineState, CliPipelineState> ApplyWindow(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string sizeStr = options.TryGetValue("size", out string? sz) ? sz : "5";
            string? slideStr = options.TryGetValue("slide", out string? sl) ? sl : null;

            // Check if time-based (contains 's' suffix)
            if (sizeStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                int sizeSeconds = int.Parse(sizeStr.TrimEnd('s', 'S'));
                TimeSpan size = TimeSpan.FromSeconds(sizeSeconds);

                if (slideStr != null && slideStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    int slideSeconds = int.Parse(slideStr.TrimEnd('s', 'S'));
                    TimeSpan slide = TimeSpan.FromSeconds(slideSeconds);
                    s.ActiveStream = s.ActiveStream.Window(size, slide).Select(w => (object)w);
                }
                else
                {
                    s.ActiveStream = s.ActiveStream.Window(size).Select(w => (object)w);
                }
            }
            else
            {
                // Count-based windowing
                int size = int.Parse(sizeStr);
                if (slideStr != null)
                {
                    int slide = int.Parse(slideStr);
                    s.ActiveStream = s.ActiveStream.Window(size, slide).Select(w => (object)w);
                }
                else
                {
                    s.ActiveStream = s.ActiveStream.Window(size).Select(w => (object)w);
                }
            }

            s.Branch = s.Branch.WithIngestEvent($"stream:window:size={sizeStr}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Window applied: size={sizeStr}, slide={slideStr ?? "none"}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies aggregations to windowed streams.
    /// Supports: count, sum, mean, min, max, collect
    /// Args: 'count' or 'count,mean' or 'sum|field=value'
    /// </summary>
    [PipelineToken("StreamAggregate", "Aggregate")]
    public static Step<CliPipelineState, CliPipelineState> ApplyAggregate(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            string raw = ParseString(args);
            string[] operations = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string op in operations)
            {
                s.ActiveStream = op.ToLowerInvariant() switch
                {
                    "count" => ApplyCountAggregate(s.ActiveStream),
                    "sum" => ApplySumAggregate(s.ActiveStream),
                    "mean" or "avg" => ApplyMeanAggregate(s.ActiveStream),
                    "min" => ApplyMinAggregate(s.ActiveStream),
                    "max" => ApplyMaxAggregate(s.ActiveStream),
                    "collect" => ApplyCollectAggregate(s.ActiveStream),
                    _ => s.ActiveStream
                };
            }

            s.Branch = s.Branch.WithIngestEvent($"stream:aggregate:{string.Join(",", operations)}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Aggregations applied: {string.Join(", ", operations)}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Maps/transforms elements in the stream.
    /// Args: 'func=...' (not yet implemented - placeholder for future expression support)
    /// </summary>
    [PipelineToken("StreamMap", "Map")]
    public static Step<CliPipelineState, CliPipelineState> ApplyMap(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            // For now, identity map (can be extended with expression evaluation)
            s.ActiveStream = s.ActiveStream.Select(x => x);
            s.Branch = s.Branch.WithIngestEvent("stream:map:identity", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine("[trace] Map applied: identity");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Filters elements in the stream.
    /// Args: 'predicate=...' (not yet implemented - placeholder for future expression support)
    /// </summary>
    [PipelineToken("StreamFilter", "Filter")]
    public static Step<CliPipelineState, CliPipelineState> ApplyFilter(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            // For now, accept all (can be extended with expression evaluation)
            s.ActiveStream = s.ActiveStream.Where(_ => true);
            s.Branch = s.Branch.WithIngestEvent("stream:filter:accept-all", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine("[trace] Filter applied: accept-all");
            }

            return Task.FromResult(s);
        };
}
