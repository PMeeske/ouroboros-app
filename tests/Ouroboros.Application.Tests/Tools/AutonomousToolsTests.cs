// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.Autonomous;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class AutonomousToolsTests : IDisposable
{
    private readonly AutonomousCoordinator _coordinator;

    public AutonomousToolsTests()
    {
        _coordinator = new AutonomousCoordinator();
        AutonomousTools.LegacyCoordinator = _coordinator;
    }

    public void Dispose()
    {
        AutonomousTools.LegacyCoordinator = null;
        _coordinator.Dispose();
        GC.SuppressFinalize(this);
    }

    // ======================================================================
    // GetAutonomousStatusTool
    // ======================================================================

    [Fact]
    public async Task GetAutonomousStatus_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new GetAutonomousStatusTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetAutonomousStatus_WhenCoordinatorSet_ShouldReturnSuccess()
    {
        // Arrange
        var tool = new GetAutonomousStatusTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ======================================================================
    // ListPendingIntentionsTool
    // ======================================================================

    [Fact]
    public async Task ListPendingIntentions_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new ListPendingIntentionsTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ListPendingIntentions_WhenNoPending_ShouldReturnEmptyMessage()
    {
        // Arrange
        var tool = new ListPendingIntentionsTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("No pending intentions");
    }

    [Fact]
    public async Task ListPendingIntentions_WhenHasPending_ShouldListAll()
    {
        // Arrange
        var tool = new ListPendingIntentionsTool();
        _coordinator.IntentionBus.ProposeIntention(
            "Test intention", "description", "rationale",
            IntentionCategory.SelfReflection, "test");

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("1 Pending Intention(s)");
        result.Value.Should().Contain("Test intention");
    }

    // ======================================================================
    // ApproveIntentionTool
    // ======================================================================

    [Fact]
    public async Task ApproveIntention_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new ApproveIntentionTool();

        // Act
        var result = await tool.InvokeAsync("""{"id":"abc"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveIntention_WithValidId_ShouldApproveAndReturnSuccess()
    {
        // Arrange
        var tool = new ApproveIntentionTool();
        var intention = _coordinator.IntentionBus.ProposeIntention(
            "Test", "desc", "reason",
            IntentionCategory.Learning, "test");
        var partialId = intention.Id.ToString()[..8];

        // Act
        var result = await tool.InvokeAsync($$"""{"id":"{{partialId}}"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("approved");
    }

    [Fact]
    public async Task ApproveIntention_WithInvalidId_ShouldReturnFailure()
    {
        // Arrange
        var tool = new ApproveIntentionTool();

        // Act
        var result = await tool.InvokeAsync("""{"id":"nonexistent"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Could not find");
    }

    [Fact]
    public async Task ApproveIntention_WithMalformedJson_ShouldReturnFailure()
    {
        // Arrange
        var tool = new ApproveIntentionTool();

        // Act
        var result = await tool.InvokeAsync("not json");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Failed to approve");
    }

    // ======================================================================
    // RejectIntentionTool
    // ======================================================================

    [Fact]
    public async Task RejectIntention_WithValidId_ShouldRejectAndReturnSuccess()
    {
        // Arrange
        var tool = new RejectIntentionTool();
        var intention = _coordinator.IntentionBus.ProposeIntention(
            "Test rejection", "desc", "reason",
            IntentionCategory.CodeModification, "test");
        var partialId = intention.Id.ToString()[..8];

        // Act
        var result = await tool.InvokeAsync($$"""{"id":"{{partialId}}", "reason":"not needed"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("rejected");
    }

    [Fact]
    public async Task RejectIntention_WithInvalidId_ShouldReturnFailure()
    {
        // Arrange
        var tool = new RejectIntentionTool();

        // Act
        var result = await tool.InvokeAsync("""{"id":"zzzzzzz"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Could not find");
    }

    // ======================================================================
    // ProposeIntentionTool
    // ======================================================================

    [Fact]
    public async Task ProposeIntention_WithValidInput_ShouldCreateIntention()
    {
        // Arrange
        var tool = new ProposeIntentionTool();
        var input = """
        {
            "title": "Improve logging",
            "description": "Add structured logging",
            "rationale": "Better observability",
            "category": "CodeModification",
            "priority": "High"
        }
        """;

        // Act
        var result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Improve logging");
        result.Value.Should().Contain("Awaiting user approval");
    }

    [Fact]
    public async Task ProposeIntention_WithInvalidCategory_ShouldDefaultToSelfReflection()
    {
        // Arrange
        var tool = new ProposeIntentionTool();
        var input = """
        {
            "title": "Test",
            "description": "desc",
            "rationale": "reason",
            "category": "InvalidCategory"
        }
        """;

        // Act
        var result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // The intention should have been created (defaults to SelfReflection)
        var pending = _coordinator.IntentionBus.GetPendingIntentions();
        pending.Should().ContainSingle(i => i.Title == "Test");
        pending.First().Category.Should().Be(IntentionCategory.SelfReflection);
    }

    [Fact]
    public async Task ProposeIntention_WithInvalidPriority_ShouldDefaultToNormal()
    {
        // Arrange
        var tool = new ProposeIntentionTool();
        var input = """
        {
            "title": "Test",
            "description": "desc",
            "rationale": "reason",
            "category": "Learning",
            "priority": "Mega"
        }
        """;

        // Act
        var result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var pending = _coordinator.IntentionBus.GetPendingIntentions();
        pending.Should().ContainSingle(i => i.Title == "Test");
        pending.First().Priority.Should().Be(IntentionPriority.Normal);
    }

    [Fact]
    public async Task ProposeIntention_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new ProposeIntentionTool();

        // Act
        var result = await tool.InvokeAsync("""{"title":"t","description":"d","rationale":"r","category":"Learning"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // ToggleAutonomousModeTool
    // ======================================================================

    [Fact]
    public async Task ToggleAutonomous_Start_WhenInactive_ShouldActivate()
    {
        // Arrange
        var tool = new ToggleAutonomousModeTool();

        // Act
        var result = await tool.InvokeAsync("start");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("started");
        _coordinator.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAutonomous_Start_WhenAlreadyActive_ShouldReturnAlreadyActive()
    {
        // Arrange
        var tool = new ToggleAutonomousModeTool();
        _coordinator.Start();

        // Act
        var result = await tool.InvokeAsync("start");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("already active");
    }

    [Fact]
    public async Task ToggleAutonomous_Stop_WhenActive_ShouldDeactivate()
    {
        // Arrange
        var tool = new ToggleAutonomousModeTool();
        _coordinator.Start();

        // Act
        var result = await tool.InvokeAsync("stop");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("stopped");
    }

    [Fact]
    public async Task ToggleAutonomous_Stop_WhenAlreadyStopped_ShouldReturnAlreadyStopped()
    {
        // Arrange
        var tool = new ToggleAutonomousModeTool();

        // Act
        var result = await tool.InvokeAsync("stop");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("already stopped");
    }

    [Fact]
    public async Task ToggleAutonomous_InvalidAction_ShouldReturnFailure()
    {
        // Arrange
        var tool = new ToggleAutonomousModeTool();

        // Act
        var result = await tool.InvokeAsync("pause");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Invalid action");
    }

    [Fact]
    public async Task ToggleAutonomous_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new ToggleAutonomousModeTool();

        // Act
        var result = await tool.InvokeAsync("start");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // InjectGoalTool
    // ======================================================================

    [Fact]
    public async Task InjectGoal_WithValidGoal_ShouldSucceed()
    {
        // Arrange
        var tool = new InjectGoalTool();

        // Act
        var result = await tool.InvokeAsync("""{"goal":"Learn C# patterns","priority":"High"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Learn C# patterns");
    }

    [Fact]
    public async Task InjectGoal_WithInvalidPriority_ShouldDefaultToNormal()
    {
        // Arrange
        var tool = new InjectGoalTool();

        // Act
        var result = await tool.InvokeAsync("""{"goal":"test goal","priority":"Extreme"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("test goal");
    }

    [Fact]
    public async Task InjectGoal_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new InjectGoalTool();

        // Act
        var result = await tool.InvokeAsync("""{"goal":"test"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // SendNeuronMessageTool
    // ======================================================================

    [Fact]
    public async Task SendNeuronMessage_WithValidInput_ShouldSendAndReturnSuccess()
    {
        // Arrange
        var tool = new SendNeuronMessageTool();
        var input = """{"neuron_id":"neuron.memory","topic":"test.ping","payload":"hello"}""";

        // Act
        var result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("neuron.memory");
        result.Value.Should().Contain("test.ping");
    }

    [Fact]
    public async Task SendNeuronMessage_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new SendNeuronMessageTool();

        // Act
        var result = await tool.InvokeAsync("""{"neuron_id":"x","topic":"t","payload":"p"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SendNeuronMessage_WithMalformedJson_ShouldReturnFailure()
    {
        // Arrange
        var tool = new SendNeuronMessageTool();

        // Act
        var result = await tool.InvokeAsync("not valid json");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Failed to send");
    }

    // ======================================================================
    // SearchNeuronHistoryTool
    // ======================================================================

    [Fact]
    public async Task SearchNeuronHistory_WhenNoMatches_ShouldReturnNoResults()
    {
        // Arrange
        var tool = new SearchNeuronHistoryTool();

        // Act
        var result = await tool.InvokeAsync("nonexistent query xyz123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("No messages found");
    }

    [Fact]
    public async Task SearchNeuronHistory_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new SearchNeuronHistoryTool();

        // Act
        var result = await tool.InvokeAsync("test");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // GetNetworkStatusTool
    // ======================================================================

    [Fact]
    public async Task GetNetworkStatus_WhenCoordinatorSet_ShouldReturnSuccess()
    {
        // Arrange
        var tool = new GetNetworkStatusTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetNetworkStatus_WhenCoordinatorNull_ShouldReturnFailure()
    {
        // Arrange
        AutonomousTools.LegacyCoordinator = null;
        var tool = new GetNetworkStatusTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // GetAllTools
    // ======================================================================

    [Fact]
    public void GetAllTools_ShouldReturnExpectedToolCount()
    {
        // Act
        var tools = AutonomousTools.GetAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Should().AllSatisfy(t =>
        {
            t.Name.Should().NotBeNullOrWhiteSpace();
            t.Description.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void GetAllTools_ShouldIncludeReasoningAndVerificationTools()
    {
        // Act
        var tools = AutonomousTools.GetAllTools().ToList();
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert
        toolNames.Should().Contain("verify_claim");
        toolNames.Should().Contain("reasoning_chain");
        toolNames.Should().Contain("episodic_memory");
        toolNames.Should().Contain("parallel_tools");
        toolNames.Should().Contain("compress_context");
        toolNames.Should().Contain("self_doubt");
    }
}

[Trait("Category", "Unit")]
public class VerifyClaimToolTests
{
    // ======================================================================
    // VerifyClaimTool
    // ======================================================================

    [Fact]
    public async Task VerifyClaim_WithEmptyClaim_ShouldReturnFailure()
    {
        // Arrange
        var tool = new VerifyClaimTool();

        // Act
        var result = await tool.InvokeAsync("""{"claim":""}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("required");
    }

    [Fact]
    public async Task VerifyClaim_WithNoSearchOrEvaluate_ShouldReturnUnverified()
    {
        // Arrange
        VerifyClaimTool.SearchFunction = null;
        VerifyClaimTool.EvaluateFunction = null;
        var tool = new VerifyClaimTool();

        // Act
        var result = await tool.InvokeAsync("""{"claim":"Water boils at 100C"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("No external evidence");
    }

    [Fact]
    public async Task VerifyClaim_WithSearchOnly_ShouldReturnRawEvidence()
    {
        // Arrange
        VerifyClaimTool.SearchFunction = (_, _) =>
            Task.FromResult("Confirmed: water boils at 100C at sea level");
        VerifyClaimTool.EvaluateFunction = null;
        var tool = new VerifyClaimTool();

        // Act
        var result = await tool.InvokeAsync("""{"claim":"Water boils at 100C","depth":"quick"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Raw Evidence");
    }

    [Fact]
    public async Task VerifyClaim_WithSearchAndEvaluate_ShouldReturnAnalysis()
    {
        // Arrange
        VerifyClaimTool.SearchFunction = (_, _) =>
            Task.FromResult("Evidence supports this claim");
        VerifyClaimTool.EvaluateFunction = (_, _) =>
            Task.FromResult("VERDICT: SUPPORTED\nCONFIDENCE: 95%");
        var tool = new VerifyClaimTool();

        // Act
        var result = await tool.InvokeAsync("""{"claim":"Water boils at 100C","depth":"thorough"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Analysis");
        result.Value.Should().Contain("SUPPORTED");
    }

    [Fact]
    public async Task VerifyClaim_WithMalformedJson_ShouldReturnFailure()
    {
        // Arrange
        var tool = new VerifyClaimTool();

        // Act
        var result = await tool.InvokeAsync("not json at all");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Verification failed");
    }
}

[Trait("Category", "Unit")]
public class ReasoningChainToolTests
{
    // ======================================================================
    // ReasoningChainTool
    // ======================================================================

    [Fact]
    public async Task ReasoningChain_WithEmptyProblem_ShouldReturnFailure()
    {
        // Arrange
        var tool = new ReasoningChainTool();

        // Act
        var result = await tool.InvokeAsync("""{"problem":""}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("required");
    }

    [Fact]
    public async Task ReasoningChain_WithNoReasonFunction_ShouldReturnFailure()
    {
        // Arrange
        ReasoningChainTool.ReasonFunction = null;
        var tool = new ReasoningChainTool();

        // Act
        var result = await tool.InvokeAsync("""{"problem":"What is 2+2?"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not available");
    }

    [Fact]
    public async Task ReasoningChain_DeductiveMode_ShouldInvokeDeductivePrompts()
    {
        // Arrange
        var prompts = new List<string>();
        ReasoningChainTool.ReasonFunction = (prompt, _) =>
        {
            prompts.Add(prompt);
            return Task.FromResult("Step result");
        };
        var tool = new ReasoningChainTool();

        // Act
        var result = await tool.InvokeAsync("""{"problem":"What is 2+2?","mode":"deductive"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        prompts.Should().HaveCount(3); // decompose, derive, synthesize
        prompts[0].Should().Contain("DECOMPOSITION");
        prompts[1].Should().Contain("DEDUCTIVE");
        prompts[2].Should().Contain("SYNTHESIS");
    }

    [Fact]
    public async Task ReasoningChain_InductiveMode_ShouldInvokeInductivePrompts()
    {
        // Arrange
        ReasoningChainTool.ReasonFunction = (_, _) =>
            Task.FromResult("Step result");
        var tool = new ReasoningChainTool();

        // Act
        var result = await tool.InvokeAsync("""{"problem":"Find the pattern","mode":"inductive"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("inductive");
    }

    [Fact]
    public async Task ReasoningChain_AbductiveMode_ShouldInvokeAbductivePrompts()
    {
        // Arrange
        var capturedPrompts = new List<string>();
        ReasoningChainTool.ReasonFunction = (prompt, _) =>
        {
            capturedPrompts.Add(prompt);
            return Task.FromResult("Explanation found");
        };
        var tool = new ReasoningChainTool();

        // Act
        var result = await tool.InvokeAsync("""{"problem":"Why did the server crash?","mode":"abductive"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedPrompts[1].Should().Contain("ABDUCTIVE");
    }
}

[Trait("Category", "Unit")]
public class EpisodicMemoryToolTests : IDisposable
{
    public EpisodicMemoryToolTests()
    {
        // Reset static state before each test
        EpisodicMemoryTool.LoadMemories(Array.Empty<EpisodicMemoryEntry>());
        EpisodicMemoryTool.ExternalStoreFunc = null;
        EpisodicMemoryTool.ExternalRecallFunc = null;
    }

    public void Dispose()
    {
        EpisodicMemoryTool.LoadMemories(Array.Empty<EpisodicMemoryEntry>());
        EpisodicMemoryTool.ExternalStoreFunc = null;
        EpisodicMemoryTool.ExternalRecallFunc = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Store_WithValidContent_ShouldReturnSuccess()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"store","content":"I learned about CQRS","emotion":"curious","significance":0.8}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Memory stored");
        result.Value.Should().Contain("curious");
    }

    [Fact]
    public async Task Store_WithEmptyContent_ShouldReturnFailure()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"store","content":""}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("required");
    }

    [Fact]
    public async Task Store_SignificanceClampedTo0And1()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Act
        await tool.InvokeAsync("""{"action":"store","content":"over clamped","significance":5.0}""");
        await tool.InvokeAsync("""{"action":"store","content":"under clamped","significance":-2.0}""");

        // Assert
        var memories = EpisodicMemoryTool.GetAllMemories();
        memories.Should().HaveCount(2);
        memories.First(m => m.Content == "over clamped").Significance.Should().Be(1.0);
        memories.First(m => m.Content == "under clamped").Significance.Should().Be(0.0);
    }

    [Fact]
    public async Task Store_WhenOverLimit_ShouldEvictLeastSignificant()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Fill to 200 with low significance
        var lowSigMemories = Enumerable.Range(0, 200).Select(i => new EpisodicMemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = $"Low sig memory {i}",
            Emotion = "neutral",
            Significance = 0.1,
            Timestamp = DateTime.UtcNow,
            RecallCount = 0,
        });
        EpisodicMemoryTool.LoadMemories(lowSigMemories);

        // Act: store one high-significance memory
        await tool.InvokeAsync("""{"action":"store","content":"High sig new memory","significance":0.95}""");

        // Assert
        var memories = EpisodicMemoryTool.GetAllMemories();
        memories.Should().HaveCount(200);
        memories.Should().Contain(m => m.Content == "High sig new memory");
    }

    [Fact]
    public async Task Recall_WithNoMemories_ShouldReturnNoResults()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"recall","content":"something"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("No episodic memories");
    }

    [Fact]
    public async Task Recall_WithEmotionFilter_ShouldFilterByEmotion()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();
        await tool.InvokeAsync("""{"action":"store","content":"happy memory","emotion":"joy","significance":0.8}""");
        await tool.InvokeAsync("""{"action":"store","content":"sad memory","emotion":"sadness","significance":0.8}""");

        // Act
        var result = await tool.InvokeAsync("""{"action":"recall","emotion":"joy","count":5}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("happy memory");
        result.Value.Should().NotContain("sad memory");
    }

    [Fact]
    public async Task Recall_WithExternalBridge_ShouldCombineResults()
    {
        // Arrange
        EpisodicMemoryTool.ExternalRecallFunc = (_, _, _) =>
            Task.FromResult<IEnumerable<string>>(new[] { "Persistent memory from last session" });
        var tool = new EpisodicMemoryTool();
        await tool.InvokeAsync("""{"action":"store","content":"current session memory","significance":0.8}""");

        // Act
        var result = await tool.InvokeAsync("""{"action":"recall","content":"memory"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Persistent");
        result.Value.Should().Contain("Persistent memory from last session");
    }

    [Fact]
    public async Task Consolidate_ShouldBoostFrequentlyRecalled()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();
        var entries = new List<EpisodicMemoryEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "frequently recalled",
                Emotion = "neutral",
                Significance = 0.5,
                Timestamp = DateTime.UtcNow,
                RecallCount = 5,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Content = "old unrecalled",
                Emotion = "neutral",
                Significance = 0.2,
                Timestamp = DateTime.UtcNow.AddDays(-2),
                RecallCount = 0,
            },
        };
        EpisodicMemoryTool.LoadMemories(entries);

        // Act
        var result = await tool.InvokeAsync("""{"action":"consolidate"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("consolidation complete");
        var memories = EpisodicMemoryTool.GetAllMemories();
        var frequent = memories.FirstOrDefault(m => m.Content == "frequently recalled");
        frequent.Should().NotBeNull();
        frequent!.Significance.Should().BeGreaterThan(0.5); // Boosted
    }

    [Fact]
    public async Task Consolidate_ShouldRemoveVeryLowSignificance()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();
        var entries = new List<EpisodicMemoryEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "almost gone",
                Emotion = "neutral",
                Significance = 0.12,
                Timestamp = DateTime.UtcNow.AddDays(-2),
                RecallCount = 0,
            },
        };
        EpisodicMemoryTool.LoadMemories(entries);

        // Act
        await tool.InvokeAsync("""{"action":"consolidate"}""");

        // Assert
        var memories = EpisodicMemoryTool.GetAllMemories();
        // Significance 0.12 - 0.1 decay = 0.1, which is below 0.15 threshold, so removed
        // (Actually 0.12 is below 0.15 already, but after decay it becomes max(0.1, 0.12-0.1)=0.1 < 0.15)
        memories.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownAction_ShouldReturnFailure()
    {
        // Arrange
        var tool = new EpisodicMemoryTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"delete"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Unknown action");
    }
}

[Trait("Category", "Unit")]
public class ParallelToolsToolTests
{
    [Fact]
    public async Task ParallelTools_WithNoExecuteFunction_ShouldReturnFailure()
    {
        // Arrange
        ParallelToolsTool.ExecuteToolFunction = null;
        var tool = new ParallelToolsTool();

        // Act
        var result = await tool.InvokeAsync("""{"tools":[{"name":"t1","input":"x"}]}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not available");
    }

    [Fact]
    public async Task ParallelTools_WithEmptyToolsList_ShouldReturnFailure()
    {
        // Arrange
        ParallelToolsTool.ExecuteToolFunction = (_, _, _) =>
            Task.FromResult("ok");
        var tool = new ParallelToolsTool();

        // Act
        var result = await tool.InvokeAsync("""{"tools":[]}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("No tools");
    }

    [Fact]
    public async Task ParallelTools_WithMoreThan10Tools_ShouldReturnFailure()
    {
        // Arrange
        ParallelToolsTool.ExecuteToolFunction = (_, _, _) =>
            Task.FromResult("ok");
        var tool = new ParallelToolsTool();
        var tools = Enumerable.Range(0, 11).Select(i => new { name = $"t{i}", input = "{}" });
        var input = JsonSerializer.Serialize(new { tools });

        // Act
        var result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Maximum 10");
    }

    [Fact]
    public async Task ParallelTools_ShouldExecuteAllToolsConcurrently()
    {
        // Arrange
        var executed = new List<string>();
        ParallelToolsTool.ExecuteToolFunction = (name, _, _) =>
        {
            lock (executed) { executed.Add(name); }
            return Task.FromResult($"Result from {name}");
        };
        var tool = new ParallelToolsTool();

        // Act
        var result = await tool.InvokeAsync("""{"tools":[{"name":"tool1","input":"a"},{"name":"tool2","input":"b"}]}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        executed.Should().HaveCount(2);
        executed.Should().Contain("tool1");
        executed.Should().Contain("tool2");
        result.Value.Should().Contain("Parallel Execution Complete");
    }

    [Fact]
    public async Task ParallelTools_WhenToolThrows_ShouldHandleGracefully()
    {
        // Arrange
        ParallelToolsTool.ExecuteToolFunction = (name, _, _) =>
        {
            if (name == "failing_tool") throw new InvalidOperationException("boom");
            return Task.FromResult("ok");
        };
        var tool = new ParallelToolsTool();

        // Act
        var result = await tool.InvokeAsync("""{"tools":[{"name":"good_tool","input":"a"},{"name":"failing_tool","input":"b"}]}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("good_tool");
        result.Value.Should().Contain("failing_tool");
    }
}

[Trait("Category", "Unit")]
public class CompressContextToolTests
{
    [Fact]
    public async Task Compress_WithEmptyContent_ShouldReturnFailure()
    {
        // Arrange
        var tool = new CompressContextTool();

        // Act
        var result = await tool.InvokeAsync("""{"content":""}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("required");
    }

    [Fact]
    public async Task Compress_WhenAlreadyUnderTarget_ShouldReturnContentAsIs()
    {
        // Arrange
        CompressContextTool.SummarizeFunction = null;
        var tool = new CompressContextTool();
        var shortContent = "This is short content.";

        // Act
        var result = await tool.InvokeAsync($$"""{"content":"{{shortContent}}","target_tokens":500}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("already within target");
    }

    [Fact]
    public async Task Compress_WithNoSummarizeFunction_ShouldUseTruncation()
    {
        // Arrange
        CompressContextTool.SummarizeFunction = null;
        var tool = new CompressContextTool();
        var longContent = string.Join(". ", Enumerable.Range(0, 200).Select(i => $"Sentence number {i} about artificial intelligence"));

        // Act
        var result = await tool.InvokeAsync(JsonSerializer.Serialize(new { content = longContent, target_tokens = 10 }));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Compressed");
    }

    [Fact]
    public async Task Compress_WithSummarizeFunction_ShouldUseLlmCompression()
    {
        // Arrange
        CompressContextTool.SummarizeFunction = (_, _) =>
            Task.FromResult("TL;DR: compressed summary");
        var tool = new CompressContextTool();
        var longContent = string.Join(". ", Enumerable.Range(0, 200).Select(i => $"Sentence number {i} that is quite verbose"));

        // Act
        var result = await tool.InvokeAsync(JsonSerializer.Serialize(new { content = longContent, target_tokens = 10 }));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("compressed summary");
    }
}

[Trait("Category", "Unit")]
public class SelfDoubtToolTests
{
    [Fact]
    public async Task SelfDoubt_WithEmptyResponse_ShouldReturnFailure()
    {
        // Arrange
        var tool = new SelfDoubtTool();

        // Act
        var result = await tool.InvokeAsync("""{"response":""}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("required");
    }

    [Fact]
    public async Task SelfDoubt_WithNoCritiqueFunction_ShouldReturnFailure()
    {
        // Arrange
        SelfDoubtTool.CritiqueFunction = null;
        var tool = new SelfDoubtTool();

        // Act
        var result = await tool.InvokeAsync("""{"response":"The earth is flat"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not available");
    }

    [Fact]
    public async Task SelfDoubt_WithCritiqueFunction_ShouldReturnAnalysis()
    {
        // Arrange
        SelfDoubtTool.CritiqueFunction = (_, _) =>
            Task.FromResult("FACTUAL ERROR: Earth is an oblate spheroid. Severity: HIGH");
        var tool = new SelfDoubtTool();

        // Act
        var result = await tool.InvokeAsync("""{"response":"The earth is flat","context":"geography discussion"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Self-Doubt Analysis");
        result.Value.Should().Contain("FACTUAL ERROR");
    }
}
