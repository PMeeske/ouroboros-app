using MediatR;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.CLI.Mediator;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Tests.CLI.Mediator.Handlers;

/// <summary>
/// Verifies that all MediatR handler types implement the correct interfaces,
/// are sealed, and have the expected constructor signatures.
/// </summary>
[Trait("Category", "Unit")]
public class HandlerTypeTests
{
    // ── Type-level checks: each handler implements IRequestHandler<TRequest, TResponse> ──

    [Fact]
    public void AffectCommandHandler_ImplementsRequestHandler()
    {
        typeof(AffectCommandHandler).Should()
            .Implement(typeof(IRequestHandler<AffectCommandRequest, string>));
    }

    [Fact]
    public void AffectCommandHandler_IsSealed()
    {
        typeof(AffectCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AgiWarmupHandler_ImplementsRequestHandler()
    {
        typeof(AgiWarmupHandler).Should()
            .Implement(typeof(IRequestHandler<AgiWarmupRequest, Unit>));
    }

    [Fact]
    public void AgiWarmupHandler_IsSealed()
    {
        typeof(AgiWarmupHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeNeuronsHandler_ImplementsRequestHandler()
    {
        typeof(AnalyzeNeuronsHandler).Should()
            .Implement(typeof(IRequestHandler<AnalyzeNeuronsRequest, IReadOnlyList<NeuronBlueprint>>));
    }

    [Fact]
    public void AnalyzeNeuronsHandler_IsSealed()
    {
        typeof(AnalyzeNeuronsHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AskResultHandler_ImplementsRequestHandler()
    {
        typeof(AskResultHandler).Should()
            .Implement(typeof(IRequestHandler<AskResultRequest, Result<string, string>>));
    }

    [Fact]
    public void AskResultHandler_IsSealed()
    {
        typeof(AskResultHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AssembleNeuronHandler_ImplementsRequestHandler()
    {
        typeof(AssembleNeuronHandler).Should()
            .Implement(typeof(IRequestHandler<AssembleNeuronRequest, Neuron?>));
    }

    [Fact]
    public void AssembleNeuronHandler_IsSealed()
    {
        typeof(AssembleNeuronHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ChatHandler_ImplementsRequestHandler()
    {
        typeof(ChatHandler).Should()
            .Implement(typeof(IRequestHandler<ChatRequest, string>));
    }

    [Fact]
    public void ChatHandler_IsSealed()
    {
        typeof(ChatHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void CreateToolHandler_ImplementsRequestHandler()
    {
        typeof(CreateToolHandler).Should()
            .Implement(typeof(IRequestHandler<CreateToolRequest, string>));
    }

    [Fact]
    public void CreateToolHandler_IsSealed()
    {
        typeof(CreateToolHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void DagCommandHandler_ImplementsRequestHandler()
    {
        typeof(DagCommandHandler).Should()
            .Implement(typeof(IRequestHandler<DagCommandRequest, string>));
    }

    [Fact]
    public void DagCommandHandler_IsSealed()
    {
        typeof(DagCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void EnvironmentCommandHandler_ImplementsRequestHandler()
    {
        typeof(EnvironmentCommandHandler).Should()
            .Implement(typeof(IRequestHandler<EnvironmentCommandRequest, string>));
    }

    [Fact]
    public void EnvironmentCommandHandler_IsSealed()
    {
        typeof(EnvironmentCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ExecuteGoalHandler_ImplementsRequestHandler()
    {
        typeof(ExecuteGoalHandler).Should()
            .Implement(typeof(IRequestHandler<ExecuteGoalRequest, string>));
    }

    [Fact]
    public void ExecuteGoalHandler_IsSealed()
    {
        typeof(ExecuteGoalHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void FetchResearchHandler_ImplementsRequestHandler()
    {
        typeof(FetchResearchHandler).Should()
            .Implement(typeof(IRequestHandler<FetchResearchRequest, string>));
    }

    [Fact]
    public void FetchResearchHandler_IsSealed()
    {
        typeof(FetchResearchHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void GetGreetingHandler_ImplementsRequestHandler()
    {
        typeof(GetGreetingHandler).Should()
            .Implement(typeof(IRequestHandler<GetGreetingRequest, string>));
    }

    [Fact]
    public void GetGreetingHandler_IsSealed()
    {
        typeof(GetGreetingHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void GetInputWithVoiceHandler_ImplementsRequestHandler()
    {
        typeof(GetInputWithVoiceHandler).Should()
            .Implement(typeof(IRequestHandler<GetInputWithVoiceRequest, string>));
    }

    [Fact]
    public void GetInputWithVoiceHandler_IsSealed()
    {
        typeof(GetInputWithVoiceHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void HandlePresenceHandler_ImplementsRequestHandler()
    {
        typeof(HandlePresenceHandler).Should()
            .Implement(typeof(IRequestHandler<HandlePresenceRequest, Unit>));
    }

    [Fact]
    public void HandlePresenceHandler_IsSealed()
    {
        typeof(HandlePresenceHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void LearnTopicHandler_ImplementsRequestHandler()
    {
        typeof(LearnTopicHandler).Should()
            .Implement(typeof(IRequestHandler<LearnTopicRequest, string>));
    }

    [Fact]
    public void LearnTopicHandler_IsSealed()
    {
        typeof(LearnTopicHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ListSkillsHandler_ImplementsRequestHandler()
    {
        typeof(ListSkillsHandler).Should()
            .Implement(typeof(IRequestHandler<ListSkillsRequest, string>));
    }

    [Fact]
    public void ListSkillsHandler_IsSealed()
    {
        typeof(ListSkillsHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void MaintenanceCommandHandler_ImplementsRequestHandler()
    {
        typeof(MaintenanceCommandHandler).Should()
            .Implement(typeof(IRequestHandler<MaintenanceCommandRequest, string>));
    }

    [Fact]
    public void MaintenanceCommandHandler_IsSealed()
    {
        typeof(MaintenanceCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void NetworkCommandHandler_ImplementsRequestHandler()
    {
        typeof(NetworkCommandHandler).Should()
            .Implement(typeof(IRequestHandler<NetworkCommandRequest, string>));
    }

    [Fact]
    public void NetworkCommandHandler_IsSealed()
    {
        typeof(NetworkCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void OrchestrateHandler_ImplementsRequestHandler()
    {
        typeof(OrchestrateHandler).Should()
            .Implement(typeof(IRequestHandler<OrchestrateRequest, string>));
    }

    [Fact]
    public void OrchestrateHandler_IsSealed()
    {
        typeof(OrchestrateHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void OrchestrationHandler_ImplementsRequestHandler()
    {
        typeof(OrchestrationHandler).Should()
            .Implement(typeof(IRequestHandler<OrchestrationRequest, string>));
    }

    [Fact]
    public void OrchestrationHandler_IsSealed()
    {
        typeof(OrchestrationHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void PersistThoughtHandler_ImplementsRequestHandler()
    {
        typeof(PersistThoughtHandler).Should()
            .Implement(typeof(IRequestHandler<PersistThoughtRequest, Unit>));
    }

    [Fact]
    public void PersistThoughtHandler_IsSealed()
    {
        typeof(PersistThoughtHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void PersistThoughtResultHandler_ImplementsRequestHandler()
    {
        typeof(PersistThoughtResultHandler).Should()
            .Implement(typeof(IRequestHandler<PersistThoughtResultRequest, Unit>));
    }

    [Fact]
    public void PersistThoughtResultHandler_IsSealed()
    {
        typeof(PersistThoughtResultHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void PlanHandler_ImplementsRequestHandler()
    {
        typeof(PlanHandler).Should()
            .Implement(typeof(IRequestHandler<PlanRequest, string>));
    }

    [Fact]
    public void PlanHandler_IsSealed()
    {
        typeof(PlanHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void PolicyCommandHandler_ImplementsRequestHandler()
    {
        typeof(PolicyCommandHandler).Should()
            .Implement(typeof(IRequestHandler<PolicyCommandRequest, string>));
    }

    [Fact]
    public void PolicyCommandHandler_IsSealed()
    {
        typeof(PolicyCommandHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessDslHandler_ImplementsRequestHandler()
    {
        typeof(ProcessDslHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessDslRequest, string>));
    }

    [Fact]
    public void ProcessDslHandler_IsSealed()
    {
        typeof(ProcessDslHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessGoalHandler_ImplementsRequestHandler()
    {
        typeof(ProcessGoalHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessGoalRequest>));
    }

    [Fact]
    public void ProcessGoalHandler_IsSealed()
    {
        typeof(ProcessGoalHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessInputHandler_ImplementsRequestHandler()
    {
        typeof(ProcessInputHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessInputRequest, string>));
    }

    [Fact]
    public void ProcessInputHandler_IsSealed()
    {
        typeof(ProcessInputHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessInputPipingHandler_ImplementsRequestHandler()
    {
        typeof(ProcessInputPipingHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessInputPipingRequest, string>));
    }

    [Fact]
    public void ProcessInputPipingHandler_IsSealed()
    {
        typeof(ProcessInputPipingHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessLargeInputHandler_ImplementsRequestHandler()
    {
        typeof(ProcessLargeInputHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessLargeInputRequest, string>));
    }

    [Fact]
    public void ProcessLargeInputHandler_IsSealed()
    {
        typeof(ProcessLargeInputHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessLargeTextHandler_ImplementsRequestHandler()
    {
        typeof(ProcessLargeTextHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessLargeTextRequest, string>));
    }

    [Fact]
    public void ProcessLargeTextHandler_IsSealed()
    {
        typeof(ProcessLargeTextHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ProcessQuestionHandler_ImplementsRequestHandler()
    {
        typeof(ProcessQuestionHandler).Should()
            .Implement(typeof(IRequestHandler<ProcessQuestionRequest, string>));
    }

    [Fact]
    public void ProcessQuestionHandler_IsSealed()
    {
        typeof(ProcessQuestionHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void QueryMeTTaHandler_ImplementsRequestHandler()
    {
        typeof(QueryMeTTaHandler).Should()
            .Implement(typeof(IRequestHandler<QueryMeTTaRequest, Result<string, string>>));
    }

    [Fact]
    public void QueryMeTTaHandler_IsSealed()
    {
        typeof(QueryMeTTaHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RecallHandler_ImplementsRequestHandler()
    {
        typeof(RecallHandler).Should()
            .Implement(typeof(IRequestHandler<RecallRequest, string>));
    }

    [Fact]
    public void RecallHandler_IsSealed()
    {
        typeof(RecallHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RememberHandler_ImplementsRequestHandler()
    {
        typeof(RememberHandler).Should()
            .Implement(typeof(IRequestHandler<RememberRequest, string>));
    }

    [Fact]
    public void RememberHandler_IsSealed()
    {
        typeof(RememberHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunAutoAgentHandler_ImplementsRequestHandler()
    {
        typeof(RunAutoAgentHandler).Should()
            .Implement(typeof(IRequestHandler<RunAutoAgentRequest, string>));
    }

    [Fact]
    public void RunAutoAgentHandler_IsSealed()
    {
        typeof(RunAutoAgentHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunMeTTaExpressionResultHandler_ImplementsRequestHandler()
    {
        typeof(RunMeTTaExpressionResultHandler).Should()
            .Implement(typeof(IRequestHandler<RunMeTTaExpressionResultRequest, Result<string, string>>));
    }

    [Fact]
    public void RunMeTTaExpressionResultHandler_IsSealed()
    {
        typeof(RunMeTTaExpressionResultHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunMeTTaRequestHandler_ImplementsRequestHandler()
    {
        typeof(RunMeTTaRequestHandler).Should()
            .Implement(typeof(IRequestHandler<RunMeTTaRequest>));
    }

    [Fact]
    public void RunMeTTaRequestHandler_IsSealed()
    {
        typeof(RunMeTTaRequestHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunPipelineResultHandler_ImplementsRequestHandler()
    {
        typeof(RunPipelineResultHandler).Should()
            .Implement(typeof(IRequestHandler<RunPipelineResultRequest, Result<string, string>>));
    }

    [Fact]
    public void RunPipelineResultHandler_IsSealed()
    {
        typeof(RunPipelineResultHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunSkillHandler_ImplementsRequestHandler()
    {
        typeof(RunSkillHandler).Should()
            .Implement(typeof(IRequestHandler<RunSkillRequest, string>));
    }

    [Fact]
    public void RunSkillHandler_IsSealed()
    {
        typeof(RunSkillHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void RunTestHandler_ImplementsRequestHandler()
    {
        typeof(RunTestHandler).Should()
            .Implement(typeof(IRequestHandler<RunTestRequest, string>));
    }

    [Fact]
    public void RunTestHandler_IsSealed()
    {
        typeof(RunTestHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void SayAndWaitHandler_ImplementsRequestHandler()
    {
        typeof(SayAndWaitHandler).Should()
            .Implement(typeof(IRequestHandler<SayAndWaitRequest, Unit>));
    }

    [Fact]
    public void SayAndWaitHandler_IsSealed()
    {
        typeof(SayAndWaitHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void SayWithVoiceHandler_ImplementsRequestHandler()
    {
        typeof(SayWithVoiceHandler).Should()
            .Implement(typeof(IRequestHandler<SayWithVoiceRequest, Unit>));
    }

    [Fact]
    public void SayWithVoiceHandler_IsSealed()
    {
        typeof(SayWithVoiceHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void StartListeningHandler_ImplementsRequestHandler()
    {
        typeof(StartListeningHandler).Should()
            .Implement(typeof(IRequestHandler<StartListeningRequest, Unit>));
    }

    [Fact]
    public void StartListeningHandler_IsSealed()
    {
        typeof(StartListeningHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void StopListeningHandler_ImplementsRequestHandler()
    {
        typeof(StopListeningHandler).Should()
            .Implement(typeof(IRequestHandler<StopListeningRequest, Unit>));
    }

    [Fact]
    public void StopListeningHandler_IsSealed()
    {
        typeof(StopListeningHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void SuggestSkillsHandler_ImplementsRequestHandler()
    {
        typeof(SuggestSkillsHandler).Should()
            .Implement(typeof(IRequestHandler<SuggestSkillsRequest, string>));
    }

    [Fact]
    public void SuggestSkillsHandler_IsSealed()
    {
        typeof(SuggestSkillsHandler).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void UseToolHandler_ImplementsRequestHandler()
    {
        typeof(UseToolHandler).Should()
            .Implement(typeof(IRequestHandler<UseToolRequest, string>));
    }

    [Fact]
    public void UseToolHandler_IsSealed()
    {
        typeof(UseToolHandler).IsSealed.Should().BeTrue();
    }

    // ── Constructor signature checks ──

    [Theory]
    [InlineData(typeof(AffectCommandHandler))]
    [InlineData(typeof(AgiWarmupHandler))]
    [InlineData(typeof(AnalyzeNeuronsHandler))]
    [InlineData(typeof(AskResultHandler))]
    [InlineData(typeof(AssembleNeuronHandler))]
    [InlineData(typeof(ChatHandler))]
    [InlineData(typeof(CreateToolHandler))]
    [InlineData(typeof(DagCommandHandler))]
    [InlineData(typeof(EnvironmentCommandHandler))]
    [InlineData(typeof(FetchResearchHandler))]
    [InlineData(typeof(GetGreetingHandler))]
    [InlineData(typeof(GetInputWithVoiceHandler))]
    [InlineData(typeof(HandlePresenceHandler))]
    [InlineData(typeof(LearnTopicHandler))]
    [InlineData(typeof(ListSkillsHandler))]
    [InlineData(typeof(MaintenanceCommandHandler))]
    [InlineData(typeof(NetworkCommandHandler))]
    [InlineData(typeof(OrchestrateHandler))]
    [InlineData(typeof(OrchestrationHandler))]
    [InlineData(typeof(PersistThoughtHandler))]
    [InlineData(typeof(PersistThoughtResultHandler))]
    [InlineData(typeof(PlanHandler))]
    [InlineData(typeof(PolicyCommandHandler))]
    [InlineData(typeof(ProcessDslHandler))]
    [InlineData(typeof(ProcessInputHandler))]
    [InlineData(typeof(ProcessInputPipingHandler))]
    [InlineData(typeof(ProcessLargeTextHandler))]
    [InlineData(typeof(RecallHandler))]
    [InlineData(typeof(RememberHandler))]
    [InlineData(typeof(RunAutoAgentHandler))]
    [InlineData(typeof(RunSkillHandler))]
    [InlineData(typeof(SayAndWaitHandler))]
    [InlineData(typeof(SayWithVoiceHandler))]
    [InlineData(typeof(StartListeningHandler))]
    [InlineData(typeof(StopListeningHandler))]
    [InlineData(typeof(SuggestSkillsHandler))]
    [InlineData(typeof(UseToolHandler))]
    public void AgentBasedHandler_HasOuroborosAgentParameter(Type handlerType)
    {
        // All agent-based handlers should accept OuroborosAgent in constructor
        var constructors = handlerType.GetConstructors();
        constructors.Should().NotBeEmpty();

        var hasAgentParam = constructors.Any(c =>
            c.GetParameters().Any(p =>
                p.ParameterType.Name == "OuroborosAgent"));
        hasAgentParam.Should().BeTrue(
            $"{handlerType.Name} should have a constructor with OuroborosAgent parameter");
    }

    [Theory]
    [InlineData(typeof(ExecuteGoalHandler))]
    [InlineData(typeof(ProcessGoalHandler))]
    [InlineData(typeof(ProcessQuestionHandler))]
    [InlineData(typeof(ProcessLargeInputHandler))]
    [InlineData(typeof(RunTestHandler))]
    public void MediatorBasedHandler_HasBothAgentAndMediatorParameters(Type handlerType)
    {
        // These handlers accept both OuroborosAgent and IMediator
        var constructors = handlerType.GetConstructors();
        constructors.Should().NotBeEmpty();

        var ctor = constructors.First();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        paramTypes.Should().Contain(t => t.Name == "OuroborosAgent");
        paramTypes.Should().Contain(t => t == typeof(IMediator));
    }

    [Fact]
    public void AskQueryHandler_HasLoggerParameter()
    {
        var ctor = typeof(AskQueryHandler).GetConstructors().First();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        paramTypes.Should().ContainSingle(t => t.IsGenericType &&
            t.GetGenericTypeDefinition().Name.Contains("ILogger"));
    }

    [Fact]
    public void RunMeTTaRequestHandler_HasServiceAndLoggerParameters()
    {
        var ctor = typeof(RunMeTTaRequestHandler).GetConstructors().First();
        var paramNames = ctor.GetParameters().Select(p => p.ParameterType.Name).ToArray();
        paramNames.Should().Contain("IMeTTaService");
    }

    [Fact]
    public void RunMeTTaExpressionResultHandler_HasParameterlessConstructor()
    {
        var ctor = typeof(RunMeTTaExpressionResultHandler).GetConstructors();
        ctor.Should().ContainSingle(c => c.GetParameters().Length == 0);
    }

    [Fact]
    public void RunPipelineResultHandler_HasParameterlessConstructor()
    {
        var ctor = typeof(RunPipelineResultHandler).GetConstructors();
        ctor.Should().ContainSingle(c => c.GetParameters().Length == 0);
    }
}
