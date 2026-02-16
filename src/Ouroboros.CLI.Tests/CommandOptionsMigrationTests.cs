using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.CLI.Commands.Options;
using Ouroboros.CLI.Hosting;
using Xunit;

namespace Ouroboros.CLI.Tests;

public class CommandOptionsMigrationTests
{
    [Fact]
    public void AskCommandOptions_ShouldHaveAllProperties()
    {
        var options = new AskCommandOptions();

        // Ask-specific
        Assert.NotNull(options.QuestionOption);
        Assert.NotNull(options.RagOption);
        Assert.NotNull(options.CultureOption);
        Assert.NotNull(options.TopKOption);

        // Composed groups
        Assert.NotNull(options.Model.ModelOption);
        Assert.NotNull(options.Model.TemperatureOption);
        Assert.NotNull(options.Model.MaxTokensOption);
        Assert.NotNull(options.Model.TimeoutSecondsOption);
        Assert.NotNull(options.Model.StreamOption);
        Assert.NotNull(options.Endpoint.EndpointOption);
        Assert.NotNull(options.Endpoint.ApiKeyOption);
        Assert.NotNull(options.Endpoint.EndpointTypeOption);
        Assert.NotNull(options.MultiModel.RouterOption);
        Assert.NotNull(options.MultiModel.CoderModelOption);
        Assert.NotNull(options.MultiModel.SummarizeModelOption);
        Assert.NotNull(options.MultiModel.ReasonModelOption);
        Assert.NotNull(options.MultiModel.GeneralModelOption);
        Assert.NotNull(options.Diagnostics.DebugOption);
        Assert.NotNull(options.Diagnostics.StrictModelOption);
        Assert.NotNull(options.Diagnostics.JsonToolsOption);
        Assert.NotNull(options.Embedding.EmbedModelOption);
        Assert.NotNull(options.Embedding.QdrantEndpointOption);
        Assert.NotNull(options.Collective.CollectiveOption);
        Assert.NotNull(options.Collective.ElectionStrategyOption);
        Assert.NotNull(options.AgentLoop.AgentOption);
        Assert.NotNull(options.AgentLoop.AgentModeOption);
        Assert.NotNull(options.AgentLoop.AgentMaxStepsOption);
        Assert.NotNull(options.Voice.VoiceOnlyOption);
        Assert.NotNull(options.Voice.LocalTtsOption);
        Assert.NotNull(options.Voice.VoiceLoopOption);
    }

    [Fact]
    public void PipelineCommandOptions_ShouldHaveAllProperties()
    {
        var options = new PipelineCommandOptions();

        // Pipeline-specific
        Assert.NotNull(options.DslOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.SourceOption);
        Assert.NotNull(options.TopKOption);
        Assert.NotNull(options.TraceOption);
        Assert.NotNull(options.CritiqueIterationsOption);

        // Composed groups
        Assert.NotNull(options.Model.ModelOption);
        Assert.NotNull(options.Model.TemperatureOption);
        Assert.NotNull(options.Model.MaxTokensOption);
        Assert.NotNull(options.Model.TimeoutSecondsOption);
        Assert.NotNull(options.Model.StreamOption);
        Assert.NotNull(options.Endpoint.EndpointOption);
        Assert.NotNull(options.Endpoint.ApiKeyOption);
        Assert.NotNull(options.Endpoint.EndpointTypeOption);
        Assert.NotNull(options.MultiModel.RouterOption);
        Assert.NotNull(options.Diagnostics.DebugOption);
        Assert.NotNull(options.Diagnostics.StrictModelOption);
        Assert.NotNull(options.Diagnostics.JsonToolsOption);
        Assert.NotNull(options.AgentLoop.AgentOption);
        Assert.NotNull(options.AgentLoop.AgentModeOption);
        Assert.NotNull(options.AgentLoop.AgentMaxStepsOption);
    }

    [Fact]
    public void OuroborosCommandOptions_ShouldHaveAllProperties()
    {
        var options = new OuroborosCommandOptions();

        // Ouroboros-specific (not composed - kept flat due to sheer number)
        Assert.NotNull(options.PersonaOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.CollectiveModeOption);
        Assert.NotNull(options.MasterModelOption);
        Assert.NotNull(options.ElectionStrategyOption);
        Assert.NotNull(options.ShowElectionOption);
        Assert.NotNull(options.ShowOptimizationOption);
        Assert.NotNull(options.VoiceOption);
        Assert.NotNull(options.PushOption);
        Assert.NotNull(options.YoloOption);
        Assert.NotNull(options.GoalOption);
        Assert.NotNull(options.CoderModelOption);
    }

    [Fact]
    public void SkillsCommandOptions_ShouldHaveAllProperties()
    {
        var options = new SkillsCommandOptions();

        // Skills-specific
        Assert.NotNull(options.ListOption);
        Assert.NotNull(options.FetchOption);
        Assert.NotNull(options.CultureOption);

        // Composed groups
        Assert.NotNull(options.Model.ModelOption);
        Assert.NotNull(options.Model.TemperatureOption);
        Assert.NotNull(options.Model.MaxTokensOption);
        Assert.NotNull(options.Model.TimeoutSecondsOption);
        Assert.NotNull(options.Model.StreamOption);
        Assert.NotNull(options.Endpoint.EndpointOption);
        Assert.NotNull(options.Endpoint.ApiKeyOption);
        Assert.NotNull(options.Endpoint.EndpointTypeOption);
        Assert.NotNull(options.MultiModel.RouterOption);
        Assert.NotNull(options.Diagnostics.DebugOption);
        Assert.NotNull(options.Diagnostics.StrictModelOption);
        Assert.NotNull(options.Diagnostics.JsonToolsOption);
        Assert.NotNull(options.Embedding.EmbedModelOption);
        Assert.NotNull(options.Embedding.QdrantEndpointOption);
    }

    [Fact]
    public void OrchestratorCommandOptions_ShouldHaveAllProperties()
    {
        var options = new OrchestratorCommandOptions();

        // Orchestrator-specific
        Assert.NotNull(options.GoalOption);
        Assert.NotNull(options.CultureOption);
        Assert.NotNull(options.EmbedOption);

        // Composed groups
        Assert.NotNull(options.Model.ModelOption);
        Assert.NotNull(options.Model.TemperatureOption);
        Assert.NotNull(options.Model.MaxTokensOption);
        Assert.NotNull(options.Model.TimeoutSecondsOption);
        Assert.NotNull(options.Model.StreamOption);
        Assert.NotNull(options.Endpoint.EndpointOption);
        Assert.NotNull(options.Endpoint.ApiKeyOption);
        Assert.NotNull(options.Endpoint.EndpointTypeOption);
        Assert.NotNull(options.MultiModel.RouterOption);
        Assert.NotNull(options.Diagnostics.DebugOption);
        Assert.NotNull(options.Diagnostics.StrictModelOption);
        Assert.NotNull(options.Diagnostics.JsonToolsOption);
        Assert.NotNull(options.Collective.CollectiveOption);
        Assert.NotNull(options.Collective.MasterModelOption);
        Assert.NotNull(options.Collective.ElectionStrategyOption);
        Assert.NotNull(options.Collective.ShowSubgoalsOption);
        Assert.NotNull(options.Collective.ParallelSubgoalsOption);
        Assert.NotNull(options.Collective.DecomposeOption);
    }

    [Fact]
    public void CognitivePhysicsCommandOptions_ShouldHaveAllProperties()
    {
        var options = new CognitivePhysicsCommandOptions();

        Assert.NotNull(options.OperationOption);
        Assert.NotNull(options.FocusOption);
        Assert.NotNull(options.TargetOption);
        Assert.NotNull(options.TargetsOption);
        Assert.NotNull(options.ResourcesOption);
        Assert.NotNull(options.ChaosIntensityOption);
        Assert.NotNull(options.ChaosResourceCostOption);
        Assert.NotNull(options.EvolutionSuccessRateOption);
        Assert.NotNull(options.EvolutionFailureRateOption);
        Assert.NotNull(options.JsonOutputOption);
        Assert.NotNull(options.VerboseOption);
    }

    [Fact]
    public void SharedOptions_ComposeCorrectly()
    {
        // Verify that shared option groups can be instantiated independently
        var model = new ModelOptions();
        var endpoint = new EndpointOptions();
        var multiModel = new MultiModelOptions();
        var diagnostics = new DiagnosticOptions();
        var embedding = new EmbeddingOptions();
        var collective = new CollectiveOptions();
        var agentLoop = new AgentLoopOptions();
        var voice = new CommandVoiceOptions();

        Assert.NotNull(model.ModelOption);
        Assert.NotNull(endpoint.EndpointOption);
        Assert.NotNull(multiModel.RouterOption);
        Assert.NotNull(diagnostics.DebugOption);
        Assert.NotNull(embedding.EmbedModelOption);
        Assert.NotNull(collective.CollectiveOption);
        Assert.NotNull(agentLoop.AgentOption);
        Assert.NotNull(voice.VoiceOnlyOption);
    }
}
