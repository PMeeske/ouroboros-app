using System.Reactive.Linq;
using System.Threading.Channels;

namespace Ouroboros.Application;

/// <summary>
/// Partial class containing stream creation helpers, aggregate operators, and utility methods.
/// </summary>
public static partial class StreamingCliSteps
{
    // Stream creation helpers

    private static IObservable<object> CreateGeneratedStream(Dictionary<string, string> options, CliPipelineState state)
    {
        int count = options.TryGetValue("count", out string? cntStr) && int.TryParse(cntStr, out int cnt) ? cnt : 100;
        int intervalMs = options.TryGetValue("interval", out string? intvStr) && int.TryParse(intvStr, out int intv) ? intv : 100;

        return Observable.Interval(TimeSpan.FromMilliseconds(intervalMs))
            .Take(count)
            .Select(i => (object)new { Index = i, Value = i * 2, Timestamp = DateTime.UtcNow });
    }

    private static IObservable<object> CreateFileStream(Dictionary<string, string> options, CliPipelineState state)
    {
        string path = options.TryGetValue("path", out string? p) ? p : "data.txt";

        if (!File.Exists(path))
        {
            return Observable.Empty<object>();
        }

        return Observable.Create<object>(async (observer, cancellationToken) =>
        {
            try
            {
                using StreamReader reader = new StreamReader(path);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    observer.OnNext(line);
                }
                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
        catch (IOException ex)
            {
                observer.OnError(ex);
            }
        });
    }

    private static IObservable<object> CreateChannelStream(Dictionary<string, string> options, CliPipelineState state)
    {
        Channel<object> channel = Channel.CreateUnbounded<object>();

        // Example: produce some test data
        Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                await channel.Writer.WriteAsync(new { Message = $"Channel item {i}", Timestamp = DateTime.UtcNow });
                await Task.Delay(500);
            }
            channel.Writer.Complete();
        });

        return channel.Reader.AsObservable();
    }

    // Aggregate operators

    private static IObservable<object> ApplyCountAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window.Count().Select(c => (object)new { Count = c });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplySumAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Sum()
                        .Select(s => (object)new { Sum = s });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMeanAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Average()
                        .Select(avg => (object)new { Mean = avg });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMinAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Min()
                        .Select(min => (object)new { Min = min });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMaxAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Max()
                        .Select(max => (object)new { Max = max });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyCollectAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .ToList()
                        .Select(list => (object)new { Items = list, Count = list.Count });
                }
                return Observable.Return(item);
            });
    }

    // Utility methods

    private static double ExtractNumericValue(object item)
    {
        if (item == null) return 0;

        Type type = item.GetType();

        // Try to get Value property (common in anonymous types)
        System.Reflection.PropertyInfo? valueProp = type.GetProperty("Value");
        if (valueProp != null)
        {
            object? value = valueProp.GetValue(item);
            if (value is IConvertible convertible)
            {
                return Convert.ToDouble(convertible);
            }
        }

        // Try Index property
        System.Reflection.PropertyInfo? indexProp = type.GetProperty("Index");
        if (indexProp != null)
        {
            object? value = indexProp.GetValue(item);
            if (value is IConvertible convertible)
            {
                return Convert.ToDouble(convertible);
            }
        }

        // Direct conversion
        if (item is IConvertible conv)
        {
            try
            {
                return Convert.ToDouble(conv);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static string FormatStreamItem(object item)
    {
        if (item == null) return "null";

        Type type = item.GetType();

        // Handle anonymous types
        if (type.Name.Contains("AnonymousType"))
        {
            System.Reflection.PropertyInfo[] properties = type.GetProperties();
            IEnumerable<string> parts = properties.Select(p =>
            {
                object? value = p.GetValue(item);
                return $"{p.Name}={value}";
            });
            return $"{{ {string.Join(", ", parts)} }}";
        }

        return item.ToString() ?? "null";
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        System.Text.RegularExpressions.Match m = SingleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        m = DoubleQuotedStringRegex().Match(arg);
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^'(?<s>.*)'$")]
    private static partial System.Text.RegularExpressions.Regex SingleQuotedStringRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^""(?<s>.*)""$")]
    private static partial System.Text.RegularExpressions.Regex DoubleQuotedStringRegex();

    private static Dictionary<string, string> ParseKeyValueArgs(string? args)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string raw = ParseString(args);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0)
            {
                string key = part.Substring(0, idx).Trim();
                string value = part.Substring(idx + 1).Trim();
                map[key] = value;
            }
            else
            {
                map[part.Trim()] = "true";
            }
        }

        return map;
    }
}
