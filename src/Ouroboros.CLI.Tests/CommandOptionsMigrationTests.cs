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
        // Arrange
        var options = new AskCommandOptions();

        // Act & Assert
        Assert.NotNull(options.QuestionOption);
        Assert.NotNull(options.RagOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.RouterOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.AgentOption);
        Assert.NotNull(options.AgentModeOption);
        Assert.NotNull(options.AgentMaxStepsOption);
        Assert.NotNull(options.StrictModelOption);
        Assert.NotNull(options.JsonToolsOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
    }

    [Fact]
    public void PipelineCommandOptions_ShouldHaveAllProperties()
    {
        // Arrange
        var options = new PipelineCommandOptions();

        // Act & Assert
        Assert.NotNull(options.DslOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.SourceOption);
        Assert.NotNull(options.TopKOption);
        Assert.NotNull(options.TraceOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.RouterOption);
        Assert.NotNull(options.AgentOption);
        Assert.NotNull(options.AgentModeOption);
        Assert.NotNull(options.AgentMaxStepsOption);
        Assert.NotNull(options.CritiqueIterationsOption);
        Assert.NotNull(options.StrictModelOption);
        Assert.NotNull(options.JsonToolsOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
    }

    [Fact]
    public void OuroborosCommandOptions_ShouldHaveAllProperties()
    {
        // Arrange
        var options = new OuroborosCommandOptions();

        // Act & Assert
        Assert.NotNull(options.PersonaOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.RouterOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.StrictModelOption);
        Assert.NotNull(options.JsonToolsOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
        Assert.NotNull(options.DecomposeOption);
        Assert.NotNull(options.CollectiveOption);
        Assert.NotNull(options.MasterModelOption);
        Assert.NotNull(options.ElectionStrategyOption);
        Assert.NotNull(options.ShowSubgoalsOption);
        Assert.NotNull(options.ParallelSubgoalsOption);
    }

    [Fact]
    public void SkillsCommandOptions_ShouldHaveAllProperties()
    {
        // Arrange
        var options = new SkillsCommandOptions();

        // Act & Assert
        Assert.NotNull(options.ListOption);
        Assert.NotNull(options.FetchOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.RouterOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.StrictModelOption);
        Assert.NotNull(options.JsonToolsOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
        Assert.NotNull(options.QdrantEndpointOption);
    }

    [Fact]
    public void OrchestratorCommandOptions_ShouldHaveAllProperties()
    {
        // Arrange
        var options = new OrchestratorCommandOptions();

        // Act & Assert
        Assert.NotNull(options.GoalOption);
        Assert.NotNull(options.ModelOption);
        Assert.NotNull(options.EmbedOption);
        Assert.NotNull(options.TemperatureOption);
        Assert.NotNull(options.MaxTokensOption);
        Assert.NotNull(options.TimeoutSecondsOption);
        Assert.NotNull(options.StreamOption);
        Assert.NotNull(options.RouterOption);
        Assert.NotNull(options.DebugOption);
        Assert.NotNull(options.StrictModelOption);
        Assert.NotNull(options.JsonToolsOption);
        Assert.NotNull(options.EndpointOption);
        Assert.NotNull(options.ApiKeyOption);
        Assert.NotNull(options.EndpointTypeOption);
        Assert.NotNull(options.DecomposeOption);
        Assert.NotNull(options.CollectiveOption);
        Assert.NotNull(options.MasterModelOption);
        Assert.NotNull(options.ElectionStrategyOption);
        Assert.NotNull(options.ShowSubgoalsOption);
        Assert.NotNull(options.ParallelSubgoalsOption);
    }

    [Fact]
    public void ServiceCollectionExtensions_ShouldRegisterCommandHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCommandHandlers();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider.GetService<Ouroboros.CLI.Commands.Handlers.AskCommandHandler>();
        Assert.NotNull(handler);
    }
}