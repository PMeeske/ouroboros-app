using FluentAssertions;
using Ouroboros.Application.Integration;
using Xunit;

namespace Ouroboros.Tests.Integration;

[Trait("Category", "Unit")]
public class IntegrationConfigTests
{
    // --- AdapterLearningConfig ---

    [Fact]
    public void AdapterLearningConfig_ShouldHaveDefaults()
    {
        var config = new AdapterLearningConfig();

        config.RankDimension.Should().Be(8);
        config.LearningRate.Should().Be(0.001);
        config.MaxAdapters.Should().Be(10);
        config.EnablePruning.Should().BeTrue();
    }

    // --- AdapterLearningOptions ---

    [Fact]
    public void AdapterLearningOptions_ShouldHaveDefaults()
    {
        var options = new AdapterLearningOptions();

        options.Rank.Should().Be(8);
        options.LearningRate.Should().Be(0.0001);
        options.BatchSize.Should().Be(32);
    }

    // --- BenchmarkConfig ---

    [Fact]
    public void BenchmarkConfig_ShouldHaveDefaults()
    {
        var config = new BenchmarkConfig();

        config.EnabledBenchmarks.Should().BeEmpty();
        config.BenchmarkTimeoutSeconds.Should().Be(300);
    }

    // --- CausalConfig ---

    [Fact]
    public void CausalConfig_ShouldHaveDefaults()
    {
        var config = new CausalConfig();

        config.DiscoveryAlgorithm.Should().Be("PC");
        config.MaxCausalComplexity.Should().Be(100);
        config.EnableCounterfactuals.Should().BeTrue();
    }

    // --- CausalReasoningOptions ---

    [Fact]
    public void CausalReasoningOptions_ShouldHaveDefaults()
    {
        var options = new CausalReasoningOptions();

        options.EnableInterventions.Should().BeTrue();
        options.EnableCounterfactuals.Should().BeTrue();
        options.MaxCausalDepth.Should().Be(5);
    }

    // --- CognitiveLoopConfig ---

    [Fact]
    public void CognitiveLoopConfig_Default_ShouldHaveExpectedValues()
    {
        var config = CognitiveLoopConfig.Default;

        config.CycleInterval.Should().Be(TimeSpan.FromSeconds(1));
        config.EnablePerception.Should().BeTrue();
        config.EnableReasoning.Should().BeTrue();
        config.EnableAction.Should().BeTrue();
        config.MaxCyclesPerRun.Should().Be(-1);
        config.AttentionThreshold.Should().Be(0.5);
    }

    // --- CognitiveLoopOptions ---

    [Fact]
    public void CognitiveLoopOptions_Default_ShouldHaveExpectedValues()
    {
        var options = CognitiveLoopOptions.Default;

        options.CycleInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.AutoStart.Should().BeFalse();
        options.MaxCyclesPerRun.Should().Be(-1);
    }

    // --- CognitiveLoopSettings ---

    [Fact]
    public void CognitiveLoopSettings_ShouldHaveDefaults()
    {
        var settings = new CognitiveLoopSettings();

        settings.CycleIntervalMs.Should().Be(1000);
        settings.MaxConcurrentGoals.Should().Be(5);
        settings.EnableAutonomousLearning.Should().BeTrue();
    }

    // --- ConsciousnessConfig ---

    [Fact]
    public void ConsciousnessConfig_ShouldHaveDefaults()
    {
        var config = new ConsciousnessConfig();

        config.MaxWorkspaceSize.Should().Be(100);
        config.ItemLifetimeMinutes.Should().Be(60);
        config.EnableMetacognition.Should().BeTrue();
    }

    // --- ConsciousnessOptions ---

    [Fact]
    public void ConsciousnessOptions_Default_ShouldHaveExpectedValues()
    {
        var options = ConsciousnessOptions.Default;

        options.MaxWorkspaceSize.Should().Be(100);
        options.DefaultItemLifetime.Should().Be(TimeSpan.FromMinutes(5));
        options.MinAttentionThreshold.Should().Be(0.5);
    }

    // --- EmbodiedAgentOptions ---

    [Fact]
    public void EmbodiedAgentOptions_ShouldHaveDefaults()
    {
        var options = new EmbodiedAgentOptions();

        options.EnvironmentType.Should().Be("Simulated");
        options.SensorDimensions.Should().Be(64);
        options.ActuatorDimensions.Should().Be(32);
    }

    // --- EmbodiedConfig ---

    [Fact]
    public void EmbodiedConfig_ShouldHaveDefaults()
    {
        var config = new EmbodiedConfig();

        config.Environment.Should().Be("gym");
        config.EnablePhysicsSimulation.Should().BeTrue();
        config.MaxSimulationSteps.Should().Be(1000);
    }

    // --- EpisodicMemoryConfig ---

    [Fact]
    public void EpisodicMemoryConfig_ShouldHaveDefaults()
    {
        var config = new EpisodicMemoryConfig();

        config.VectorStoreConnectionString.Should().Be("http://localhost:6333");
        config.MaxMemorySize.Should().Be(10000);
        config.ConsolidationIntervalHours.Should().Be(24);
        config.EnableAutoConsolidation.Should().BeTrue();
    }

    // --- EpisodicMemoryOptions ---

    [Fact]
    public void EpisodicMemoryOptions_ShouldHaveDefaults()
    {
        var options = new EpisodicMemoryOptions();

        options.VectorStoreType.Should().Be("InMemory");
        options.MaxEpisodes.Should().Be(10000);
        options.SimilarityThreshold.Should().Be(0.7);
    }

    // --- ExecutionConfig ---

    [Fact]
    public void ExecutionConfig_Default_ShouldHaveExpectedValues()
    {
        var config = ExecutionConfig.Default;

        config.UseEpisodicMemory.Should().BeTrue();
        config.UseCausalReasoning.Should().BeTrue();
        config.UseHierarchicalPlanning.Should().BeTrue();
        config.UseWorldModel.Should().BeFalse();
        config.MaxPlanningDepth.Should().Be(10);
    }

    // --- FeaturesConfig ---

    [Fact]
    public void FeaturesConfig_ShouldHaveExpectedDefaults()
    {
        var config = new FeaturesConfig();

        config.EnableEpisodicMemory.Should().BeTrue();
        config.EnableAdapterLearning.Should().BeTrue();
        config.EnableMeTTa.Should().BeTrue();
        config.EnablePlanning.Should().BeTrue();
        config.EnableReflection.Should().BeTrue();
        config.EnableSynthesis.Should().BeFalse();
        config.EnableWorldModel.Should().BeFalse();
        config.EnableMultiAgent.Should().BeFalse();
        config.EnableCausal.Should().BeTrue();
        config.EnableMetaLearning.Should().BeFalse();
        config.EnableEmbodied.Should().BeFalse();
        config.EnableConsciousness.Should().BeTrue();
        config.EnableBenchmarks.Should().BeTrue();
        config.EnableCognitiveLoop.Should().BeFalse();
    }

    // --- HierarchicalPlanningOptions ---

    [Fact]
    public void HierarchicalPlanningOptions_ShouldHaveDefaults()
    {
        var options = new HierarchicalPlanningOptions();

        options.MaxDepth.Should().Be(10);
        options.MinStepsForDecomposition.Should().Be(3);
        options.ComplexityThreshold.Should().Be(0.7);
    }

    // --- MeTTaConfig ---

    [Fact]
    public void MeTTaConfig_ShouldHaveDefaults()
    {
        var config = new MeTTaConfig();

        config.ExecutablePath.Should().Be("./metta");
        config.MaxInferenceSteps.Should().Be(100);
        config.EnableTypeChecking.Should().BeTrue();
        config.EnableAbduction.Should().BeTrue();
    }

    // --- MeTTaReasoningOptions ---

    [Fact]
    public void MeTTaReasoningOptions_ShouldHaveDefaults()
    {
        var options = new MeTTaReasoningOptions();

        options.HyperonPath.Should().BeEmpty();
        options.MaxInferenceSteps.Should().Be(100);
        options.ConfidenceThreshold.Should().Be(0.7);
    }

    // --- MetaLearningConfig ---

    [Fact]
    public void MetaLearningConfig_ShouldHaveDefaults()
    {
        var config = new MetaLearningConfig();

        config.Algorithm.Should().Be("MAML");
        config.MetaLearningSteps.Should().Be(5);
        config.MetaLearningRate.Should().Be(0.001);
    }

    // --- MetaLearningOptions ---

    [Fact]
    public void MetaLearningOptions_ShouldHaveDefaults()
    {
        var options = new MetaLearningOptions();

        options.Algorithm.Should().Be("MAML");
        options.InnerSteps.Should().Be(5);
        options.MetaLearningRate.Should().Be(0.001);
    }

    // --- MultiAgentConfig ---

    [Fact]
    public void MultiAgentConfig_ShouldHaveDefaults()
    {
        var config = new MultiAgentConfig();

        config.MaxAgents.Should().Be(10);
        config.ConsensusProtocol.Should().Be("raft");
        config.EnableKnowledgeSharing.Should().BeTrue();
    }

    // --- MultiAgentOptions ---

    [Fact]
    public void MultiAgentOptions_ShouldHaveDefaults()
    {
        var options = new MultiAgentOptions();

        options.MaxAgents.Should().Be(10);
        options.CoordinationStrategy.Should().Be("Hierarchical");
        options.EnableCommunication.Should().BeTrue();
    }

    // --- PlanningConfig ---

    [Fact]
    public void PlanningConfig_ShouldHaveDefaults()
    {
        var config = new PlanningConfig();

        config.MaxPlanningDepth.Should().Be(10);
        config.MaxPlanningTimeSeconds.Should().Be(30);
        config.EnableHTN.Should().BeTrue();
        config.EnableTemporalPlanning.Should().BeTrue();
    }

    // --- ProgramSynthesisOptions ---

    [Fact]
    public void ProgramSynthesisOptions_ShouldHaveDefaults()
    {
        var options = new ProgramSynthesisOptions();

        options.TargetLanguage.Should().Be("CSharp");
        options.MaxSynthesisAttempts.Should().Be(5);
        options.EnableVerification.Should().BeTrue();
    }

    // --- ReasoningConfig ---

    [Fact]
    public void ReasoningConfig_Default_ShouldHaveExpectedValues()
    {
        var config = ReasoningConfig.Default;

        config.UseSymbolicReasoning.Should().BeTrue();
        config.UseCausalInference.Should().BeTrue();
        config.UseAbduction.Should().BeTrue();
        config.MaxInferenceSteps.Should().Be(100);
    }

    // --- ReflectionConfig ---

    [Fact]
    public void ReflectionConfig_ShouldHaveDefaults()
    {
        var config = new ReflectionConfig();

        config.AnalysisWindowHours.Should().Be(24);
        config.MinEpisodesForAnalysis.Should().Be(10);
        config.EnableAutoImprovement.Should().BeTrue();
    }

    // --- ReflectionOptions ---

    [Fact]
    public void ReflectionOptions_ShouldHaveDefaults()
    {
        var options = new ReflectionOptions();

        options.EnableCodeReflection.Should().BeTrue();
        options.EnablePerformanceReflection.Should().BeTrue();
        options.ReflectionDepth.Should().Be(3);
    }

    // --- SynthesisConfig ---

    [Fact]
    public void SynthesisConfig_ShouldHaveDefaults()
    {
        var config = new SynthesisConfig();

        config.MaxSynthesisTimeSeconds.Should().Be(60);
        config.EnableLibraryLearning.Should().BeTrue();
        config.MaxProgramComplexity.Should().Be(100);
    }

    // --- WorldModelConfig ---

    [Fact]
    public void WorldModelConfig_ShouldHaveDefaults()
    {
        var config = new WorldModelConfig();

        config.ModelArchitecture.Should().Be("transformer");
        config.MaxModelComplexity.Should().Be(1000000);
        config.EnableImaginationPlanning.Should().BeTrue();
    }

    // --- WorldModelOptions ---

    [Fact]
    public void WorldModelOptions_ShouldHaveDefaults()
    {
        var options = new WorldModelOptions();

        options.StateSpaceSize.Should().Be(128);
        options.ActionSpaceSize.Should().Be(64);
        options.DiscountFactor.Should().Be(0.99);
    }
}
