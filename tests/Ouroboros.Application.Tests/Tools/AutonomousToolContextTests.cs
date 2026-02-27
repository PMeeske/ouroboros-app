// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.Autonomous;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class AutonomousToolContextTests : IDisposable
{
    private readonly AutonomousToolContext _context;
    private readonly AutonomousCoordinator _coordinator;
    private readonly IAutonomousToolContext _savedDefault;

    public AutonomousToolContextTests()
    {
        _savedDefault = AutonomousTools.DefaultContext;
        _context = new AutonomousToolContext();
        _coordinator = new AutonomousCoordinator();
    }

    public void Dispose()
    {
        AutonomousTools.DefaultContext = _savedDefault;
        _coordinator.Dispose();
        GC.SuppressFinalize(this);
    }

    // ======================================================================
    // Context properties are get/settable
    // ======================================================================

    [Fact]
    public void Coordinator_CanSetAndGet()
    {
        // Act
        _context.Coordinator = _coordinator;

        // Assert
        _context.Coordinator.Should().BeSameAs(_coordinator);
    }

    [Fact]
    public void MeTTaOrchestrator_CanSetAndGet()
    {
        // Arrange
        var orchestrator = new Ouroboros.Application.Services.ParallelMeTTaThoughtStreams(2);

        // Act
        _context.MeTTaOrchestrator = orchestrator;

        // Assert
        _context.MeTTaOrchestrator.Should().BeSameAs(orchestrator);
    }

    [Fact]
    public void PipelineState_CanSetAndGet()
    {
        // Arrange — PipelineState is nullable, just verify round-trip
        _context.PipelineState.Should().BeNull();

        // Act / Assert
        // PipelineState is set to null by default, which is expected
        _context.PipelineState = null;
        _context.PipelineState.Should().BeNull();
    }

    [Fact]
    public void OllamaFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("ollama");

        // Act
        _context.OllamaFunction = fn;

        // Assert
        _context.OllamaFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void SearchFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("search");

        // Act
        _context.SearchFunction = fn;

        // Assert
        _context.SearchFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void EvaluateFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("eval");

        // Act
        _context.EvaluateFunction = fn;

        // Assert
        _context.EvaluateFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void ReasonFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("reason");

        // Act
        _context.ReasonFunction = fn;

        // Assert
        _context.ReasonFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void ExecuteToolFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, string, CancellationToken, Task<string>> fn = (_, _, _) => Task.FromResult("exec");

        // Act
        _context.ExecuteToolFunction = fn;

        // Assert
        _context.ExecuteToolFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void SummarizeFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("summary");

        // Act
        _context.SummarizeFunction = fn;

        // Assert
        _context.SummarizeFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void CritiqueFunction_CanSetAndGet()
    {
        // Arrange
        Func<string, CancellationToken, Task<string>> fn = (_, _) => Task.FromResult("critique");

        // Act
        _context.CritiqueFunction = fn;

        // Assert
        _context.CritiqueFunction.Should().BeSameAs(fn);
    }

    [Fact]
    public void EpisodicExternalStoreFunc_CanSetAndGet()
    {
        // Arrange
        Func<string, string, double, CancellationToken, Task> fn = (_, _, _, _) => Task.CompletedTask;

        // Act
        _context.EpisodicExternalStoreFunc = fn;

        // Assert
        _context.EpisodicExternalStoreFunc.Should().BeSameAs(fn);
    }

    [Fact]
    public void EpisodicExternalRecallFunc_CanSetAndGet()
    {
        // Arrange
        Func<string, int, CancellationToken, Task<IEnumerable<string>>> fn =
            (_, _, _) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        // Act
        _context.EpisodicExternalRecallFunc = fn;

        // Assert
        _context.EpisodicExternalRecallFunc.Should().BeSameAs(fn);
    }

    [Fact]
    public void CognitiveEmitFunc_CanSetAndGet()
    {
        // Arrange
        Action<string> fn = _ => { };

        // Act
        _context.CognitiveEmitFunc = fn;

        // Assert
        _context.CognitiveEmitFunc.Should().BeSameAs(fn);
    }

    // ======================================================================
    // GetAllTools(context) returns all tools
    // ======================================================================

    [Fact]
    public void GetAllTools_WithContext_ShouldReturnAllExpectedTools()
    {
        // Arrange
        _context.Coordinator = _coordinator;

        // Act
        var tools = AutonomousTools.GetAllTools(_context).ToList();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Should().AllSatisfy(t =>
        {
            t.Name.Should().NotBeNullOrWhiteSpace();
            t.Description.Should().NotBeNullOrWhiteSpace();
        });

        var toolNames = tools.Select(t => t.Name).ToList();
        toolNames.Should().Contain("autonomous_status");
        toolNames.Should().Contain("list_my_intentions");
        toolNames.Should().Contain("approve_my_intention");
        toolNames.Should().Contain("reject_my_intention");
        toolNames.Should().Contain("propose_intention");
        toolNames.Should().Contain("neural_network_status");
        toolNames.Should().Contain("send_to_neuron");
        toolNames.Should().Contain("toggle_autonomous");
        toolNames.Should().Contain("set_autonomous_goal");
        toolNames.Should().Contain("search_neuron_history");
        toolNames.Should().Contain("verify_claim");
        toolNames.Should().Contain("reasoning_chain");
        toolNames.Should().Contain("episodic_memory");
        toolNames.Should().Contain("parallel_tools");
        toolNames.Should().Contain("compress_context");
        toolNames.Should().Contain("self_doubt");
        toolNames.Should().Contain("parallel_metta_think");
        toolNames.Should().Contain("ouroboros_metta");
    }

    [Fact]
    public void GetAllTools_ParameterlessOverload_UseDefaultContext()
    {
        // Arrange
        AutonomousTools.DefaultContext = _context;
        _context.Coordinator = _coordinator;

        // Act
        var tools = AutonomousTools.GetAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Select(t => t.Name).Should().Contain("autonomous_status");
    }

    // ======================================================================
    // Tools access coordinator through injected context
    // ======================================================================

    [Fact]
    public async Task StatusTool_WithInjectedContext_AccessesCoordinator()
    {
        // Arrange
        _context.Coordinator = _coordinator;
        var tool = new GetAutonomousStatusTool(_context);

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StatusTool_WithNullCoordinatorInContext_ReturnsFailure()
    {
        // Arrange
        _context.Coordinator = null;
        var tool = new GetAutonomousStatusTool(_context);

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not initialized");
    }

    [Fact]
    public async Task ListIntentionsTool_WithInjectedContext_AccessesCoordinator()
    {
        // Arrange
        _context.Coordinator = _coordinator;
        var tool = new ListPendingIntentionsTool(_context);

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleModeTool_WithInjectedContext_CanStartCoordinator()
    {
        // Arrange
        _context.Coordinator = _coordinator;
        var tool = new ToggleAutonomousModeTool(_context);

        // Act
        var result = await tool.InvokeAsync("start");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("started");
        _coordinator.IsActive.Should().BeTrue();
    }

    // ======================================================================
    // Context isolation: two contexts don't interfere
    // ======================================================================

    [Fact]
    public async Task TwoContexts_AreIsolated()
    {
        // Arrange
        var ctx1 = new AutonomousToolContext { Coordinator = _coordinator };
        var ctx2 = new AutonomousToolContext { Coordinator = null };

        var tool1 = new GetAutonomousStatusTool(ctx1);
        var tool2 = new GetAutonomousStatusTool(ctx2);

        // Act
        var result1 = await tool1.InvokeAsync("");
        var result2 = await tool2.InvokeAsync("");

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // DefaultContext backward compatibility facade
    // ======================================================================

    [Fact]
    public void SharedCoordinator_DelegatesToDefaultContext()
    {
        // Arrange
        AutonomousTools.DefaultContext = _context;

        // Act
        AutonomousTools.SharedCoordinator = _coordinator;

        // Assert
        _context.Coordinator.Should().BeSameAs(_coordinator);
        AutonomousTools.SharedCoordinator.Should().BeSameAs(_coordinator);
    }

    // ======================================================================
    // WithAutonomousTools extension accepts context
    // ======================================================================

    [Fact]
    public void WithAutonomousTools_WithContext_RegistersAllTools()
    {
        // Arrange
        _context.Coordinator = _coordinator;
        var registry = ToolRegistry.CreateDefault();

        // Act
        var result = registry.WithAutonomousTools(_context);

        // Assert
        var tool = result.Get("autonomous_status");
        tool.Should().NotBeNull();

        var verifyTool = result.Get("verify_claim");
        verifyTool.Should().NotBeNull();
    }
}
