// <copyright file="OuroborosConfiguration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Unified configuration for the entire Ouroboros system.
/// Supports loading from appsettings.json and environment variables.
/// </summary>
public sealed class OuroborosConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ouroboros";

    /// <summary>Gets or sets episodic memory configuration.</summary>
    public EpisodicMemoryConfig EpisodicMemory { get; set; } = new();

    /// <summary>Gets or sets adapter learning configuration.</summary>
    public AdapterLearningConfig AdapterLearning { get; set; } = new();

    /// <summary>Gets or sets MeTTa reasoning configuration.</summary>
    public MeTTaConfig MeTTa { get; set; } = new();

    /// <summary>Gets or sets hierarchical planning configuration.</summary>
    public PlanningConfig Planning { get; set; } = new();

    /// <summary>Gets or sets reflection engine configuration.</summary>
    public ReflectionConfig Reflection { get; set; } = new();

    /// <summary>Gets or sets program synthesis configuration.</summary>
    public SynthesisConfig Synthesis { get; set; } = new();

    /// <summary>Gets or sets world model configuration.</summary>
    public WorldModelConfig WorldModel { get; set; } = new();

    /// <summary>Gets or sets multi-agent configuration.</summary>
    public MultiAgentConfig MultiAgent { get; set; } = new();

    /// <summary>Gets or sets causal reasoning configuration.</summary>
    public CausalConfig Causal { get; set; } = new();

    /// <summary>Gets or sets meta-learning configuration.</summary>
    public MetaLearningConfig MetaLearning { get; set; } = new();

    /// <summary>Gets or sets embodied simulation configuration.</summary>
    public EmbodiedConfig Embodied { get; set; } = new();

    /// <summary>Gets or sets consciousness configuration.</summary>
    public ConsciousnessConfig Consciousness { get; set; } = new();

    /// <summary>Gets or sets benchmark configuration.</summary>
    public BenchmarkConfig Benchmarks { get; set; } = new();

    /// <summary>Gets or sets cognitive loop configuration.</summary>
    public CognitiveLoopSettings CognitiveLoop { get; set; } = new();

    /// <summary>Gets or sets which features are enabled.</summary>
    public FeaturesConfig Features { get; set; } = new();

    /// <summary>
    /// Loads configuration from IConfiguration.
    /// </summary>
    public static OuroborosConfiguration Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        var config = new OuroborosConfiguration();
        configuration.GetSection(SectionName).Bind(config);
        return config;
    }

    /// <summary>
    /// Validates the configuration and returns validation errors if any.
    /// </summary>
    public List<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);

        // Validate nested configurations
        ValidateNested(EpisodicMemory, "EpisodicMemory", results);
        ValidateNested(AdapterLearning, "AdapterLearning", results);
        ValidateNested(MeTTa, "MeTTa", results);
        ValidateNested(Planning, "Planning", results);
        ValidateNested(Reflection, "Reflection", results);
        ValidateNested(Synthesis, "Synthesis", results);
        ValidateNested(WorldModel, "WorldModel", results);
        ValidateNested(MultiAgent, "MultiAgent", results);
        ValidateNested(Causal, "Causal", results);
        ValidateNested(MetaLearning, "MetaLearning", results);
        ValidateNested(Embodied, "Embodied", results);
        ValidateNested(Consciousness, "Consciousness", results);
        ValidateNested(Benchmarks, "Benchmarks", results);
        ValidateNested(CognitiveLoop, "CognitiveLoop", results);

        return results;
    }

    private static void ValidateNested(object obj, string memberName, List<ValidationResult> results)
    {
        var context = new ValidationContext(obj) { MemberName = memberName };
        var nestedResults = new List<ValidationResult>();
        Validator.TryValidateObject(obj, context, nestedResults, true);
        
        foreach (var result in nestedResults)
        {
            results.Add(new ValidationResult(
                $"{memberName}.{result.ErrorMessage}",
                result.MemberNames.Select(m => $"{memberName}.{m}")));
        }
    }
}