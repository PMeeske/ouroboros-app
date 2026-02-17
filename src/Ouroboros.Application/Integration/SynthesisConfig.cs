using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Application.Integration;

/// <summary>Synthesis configuration.</summary>
public sealed class SynthesisConfig
{
    /// <summary>Gets or sets maximum synthesis time in seconds.</summary>
    [Range(10, 600)]
    public int MaxSynthesisTimeSeconds { get; set; } = 60;

    /// <summary>Gets or sets whether library learning is enabled.</summary>
    public bool EnableLibraryLearning { get; set; } = true;

    /// <summary>Gets or sets maximum program complexity.</summary>
    [Range(10, 10000)]
    public int MaxProgramComplexity { get; set; } = 100;
}