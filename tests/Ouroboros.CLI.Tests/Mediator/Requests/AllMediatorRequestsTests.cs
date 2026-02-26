using MediatR;
using Ouroboros.CLI.Mediator;
using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Mediator.Requests;

[Trait("Category", "Unit")]
public class AllMediatorRequestsTests
{
    [Fact]
    public void AffectCommandRequest_SetsSubCommand()
    {
        var req = new AffectCommandRequest("show");
        req.SubCommand.Should().Be("show");
        req.Should().BeAssignableTo<IRequest<string>>();
    }

    [Fact]
    public void AgiWarmupRequest_ImplementsIRequest()
    {
        var req = new AgiWarmupRequest();
        req.Should().BeAssignableTo<IRequest<Unit>>();
    }

    [Fact]
    public void AskResultRequest_SetsQuestion()
    {
        var req = new AskResultRequest("What is x?");
        req.Question.Should().Be("What is x?");
    }

    [Fact]
    public void CreateToolRequest_SetsToolName()
    {
        var req = new CreateToolRequest("calculator");
        req.ToolName.Should().Be("calculator");
    }

    [Fact]
    public void DagCommandRequest_SetsSubCommand()
    {
        var req = new DagCommandRequest("show");
        req.SubCommand.Should().Be("show");
    }

    [Fact]
    public void EnvironmentCommandRequest_SetsSubCommand()
    {
        var req = new EnvironmentCommandRequest("detect");
        req.SubCommand.Should().Be("detect");
    }

    [Fact]
    public void ExecuteGoalRequest_SetsGoal()
    {
        var req = new ExecuteGoalRequest("Write tests");
        req.Goal.Should().Be("Write tests");
    }

    [Fact]
    public void FetchResearchRequest_SetsQuery()
    {
        var req = new FetchResearchRequest("neural networks");
        req.Query.Should().Be("neural networks");
    }

    [Fact]
    public void GetGreetingRequest_CanBeCreated()
    {
        var req = new GetGreetingRequest();
        req.Should().BeAssignableTo<IRequest<string>>();
    }

    [Fact]
    public void GetInputWithVoiceRequest_SetsPrompt()
    {
        var req = new GetInputWithVoiceRequest("Say something");
        req.Prompt.Should().Be("Say something");
    }

    [Fact]
    public void LearnTopicRequest_SetsTopic()
    {
        var req = new LearnTopicRequest("quantum computing");
        req.Topic.Should().Be("quantum computing");
    }

    [Fact]
    public void ListSkillsRequest_CanBeCreated()
    {
        var req = new ListSkillsRequest();
        req.Should().BeAssignableTo<IRequest<string>>();
    }

    [Fact]
    public void MaintenanceCommandRequest_SetsSubCommand()
    {
        var req = new MaintenanceCommandRequest("cleanup");
        req.SubCommand.Should().Be("cleanup");
    }

    [Fact]
    public void NetworkCommandRequest_SetsSubCommand()
    {
        var req = new NetworkCommandRequest("status");
        req.SubCommand.Should().Be("status");
    }

    [Fact]
    public void OrchestrateRequest_SetsGoal()
    {
        var req = new OrchestrateRequest("Build project");
        req.Goal.Should().Be("Build project");
    }

    [Fact]
    public void OrchestrationRequest_SetsPrompt()
    {
        var req = new OrchestrationRequest("Generate code");
        req.Prompt.Should().Be("Generate code");
    }

    [Fact]
    public void PlanRequest_SetsGoal()
    {
        var req = new PlanRequest("Create API");
        req.Goal.Should().Be("Create API");
    }

    [Fact]
    public void PolicyCommandRequest_SetsSubCommand()
    {
        var req = new PolicyCommandRequest("list");
        req.SubCommand.Should().Be("list");
    }

    [Fact]
    public void ProcessDslRequest_SetsDsl()
    {
        var req = new ProcessDslRequest("ask | summarize");
        req.Dsl.Should().Be("ask | summarize");
    }

    [Fact]
    public void ProcessGoalRequest_SetsGoal()
    {
        var req = new ProcessGoalRequest("Fix bug");
        req.Goal.Should().Be("Fix bug");
    }

    [Fact]
    public void ProcessInputPipingRequest_SetsInputAndMaxDepth()
    {
        var req = new ProcessInputPipingRequest("input text", 10);
        req.Input.Should().Be("input text");
        req.MaxPipeDepth.Should().Be(10);
    }

    [Fact]
    public void ProcessInputPipingRequest_DefaultMaxPipeDepth()
    {
        var req = new ProcessInputPipingRequest("input");
        req.MaxPipeDepth.Should().Be(5);
    }

    [Fact]
    public void ProcessInputRequest_SetsInput()
    {
        var req = new ProcessInputRequest("user input");
        req.Input.Should().Be("user input");
    }

    [Fact]
    public void ProcessLargeInputRequest_SetsTaskAndLargeInput()
    {
        var req = new ProcessLargeInputRequest("summarize", "large text here");
        req.Task.Should().Be("summarize");
        req.LargeInput.Should().Be("large text here");
    }

    [Fact]
    public void ProcessLargeTextRequest_SetsInput()
    {
        var req = new ProcessLargeTextRequest("big text");
        req.Input.Should().Be("big text");
    }

    [Fact]
    public void ProcessQuestionRequest_SetsQuestion()
    {
        var req = new ProcessQuestionRequest("What is DDD?");
        req.Question.Should().Be("What is DDD?");
    }

    [Fact]
    public void QueryMeTTaRequest_SetsQuery()
    {
        var req = new QueryMeTTaRequest("(metta query)");
        req.Query.Should().Be("(metta query)");
    }

    [Fact]
    public void RunMeTTaExpressionResultRequest_SetsExpression()
    {
        var req = new RunMeTTaExpressionResultRequest("(+ 1 2)");
        req.Expression.Should().Be("(+ 1 2)");
    }

    [Fact]
    public void RunPipelineResultRequest_SetsDsl()
    {
        var req = new RunPipelineResultRequest("ask 'test' | summarize");
        req.Dsl.Should().Be("ask 'test' | summarize");
    }

    [Fact]
    public void RunSkillRequest_SetsSkillName()
    {
        var req = new RunSkillRequest("code_review");
        req.SkillName.Should().Be("code_review");
    }

    [Fact]
    public void SayAndWaitRequest_SetsText()
    {
        var req = new SayAndWaitRequest("Hello");
        req.Text.Should().Be("Hello");
        req.Persona.Should().BeNull();
    }

    [Fact]
    public void SayAndWaitRequest_WithPersona_SetsPersona()
    {
        var req = new SayAndWaitRequest("Hello", "Iaret");
        req.Persona.Should().Be("Iaret");
    }

    [Fact]
    public void SayWithVoiceRequest_SetsText()
    {
        var req = new SayWithVoiceRequest("Speak this");
        req.Text.Should().Be("Speak this");
        req.IsWhisper.Should().BeFalse();
    }

    [Fact]
    public void SayWithVoiceRequest_WithWhisper_SetsWhisper()
    {
        var req = new SayWithVoiceRequest("Quietly", true);
        req.IsWhisper.Should().BeTrue();
    }

    [Fact]
    public void StartListeningRequest_CanBeCreated()
    {
        var req = new StartListeningRequest();
        req.Should().BeAssignableTo<IRequest<Unit>>();
    }

    [Fact]
    public void StopListeningRequest_CanBeCreated()
    {
        var req = new StopListeningRequest();
        req.Should().BeAssignableTo<IRequest<Unit>>();
    }

    [Fact]
    public void SuggestSkillsRequest_SetsGoal()
    {
        var req = new SuggestSkillsRequest("code optimization");
        req.Goal.Should().Be("code optimization");
    }

    [Fact]
    public void UseToolRequest_SetsToolNameAndInput()
    {
        var req = new UseToolRequest("calculator", "2+2");
        req.ToolName.Should().Be("calculator");
        req.Input.Should().Be("2+2");
    }

    [Fact]
    public void UseToolRequest_NullInput_IsAllowed()
    {
        var req = new UseToolRequest("list_tools", null);
        req.Input.Should().BeNull();
    }
}
