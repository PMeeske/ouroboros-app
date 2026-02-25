using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Benchmark configuration.</summary>
public sealed class BenchmarkConfig
{
    /// <summary>Gets or sets enabled benchmarks.</summary>
    public List<string> EnabledBenchmarks { get; set; } = new();

    /// <summary>Gets or sets benchmark timeout in seconds.</summary>
    [Range(10, 3600)]
    public int BenchmarkTimeoutSeconds { get; set; } = 300;
}