using FluentAssertions;
using Ouroboros.Application.SelfAssembly;
using Xunit;

namespace Ouroboros.Tests.SelfAssembly;

[Trait("Category", "Unit")]
public class SelfAssemblyModelsTests
{
    [Fact]
    public void NeuronCapability_Flags_ShouldCombine()
    {
        var combined = NeuronCapability.TextProcessing | NeuronCapability.Reasoning;

        combined.HasFlag(NeuronCapability.TextProcessing).Should().BeTrue();
        combined.HasFlag(NeuronCapability.Reasoning).Should().BeTrue();
        combined.HasFlag(NeuronCapability.FileAccess).Should().BeFalse();
    }

    [Fact]
    public void MessageHandler_ShouldSetProperties()
    {
        var handler = new MessageHandler
        {
            TopicPattern = "reflection.*",
            HandlingLogic = "Process reflection request",
            SendsResponse = true,
            BroadcastsResult = false
        };

        handler.TopicPattern.Should().Be("reflection.*");
        handler.SendsResponse.Should().BeTrue();
        handler.BroadcastsResult.Should().BeFalse();
    }

    [Fact]
    public void MessageHandlerSpec_ShouldSetAllProperties()
    {
        var spec = new MessageHandlerSpec("test.topic", "HandleTest", "Test handler", "string", "bool");

        spec.Topic.Should().Be("test.topic");
        spec.HandlerName.Should().Be("HandleTest");
        spec.InputType.Should().Be("string");
        spec.OutputType.Should().Be("bool");
    }

    [Fact]
    public void SafetyConstraint_ShouldHaveDefaults()
    {
        var constraint = new SafetyConstraint
        {
            Name = "no-file-access",
            Description = "Blocks file access",
            MeTTaExpression = "(= (has-capability FileAccess) False)"
        };

        constraint.Weight.Should().Be(1.0);
        constraint.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void ConstraintResult_ShouldSetProperties()
    {
        var constraint = new SafetyConstraint
        {
            Name = "test",
            Description = "test",
            MeTTaExpression = "test"
        };
        var result = new ConstraintResult(constraint, true, null);

        result.Passed.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AssemblyProposalStatus_ShouldHaveAllValues()
    {
        Enum.GetValues<AssemblyProposalStatus>().Should().HaveCount(7);
    }

    [Fact]
    public void AssemblyState_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var state = new AssemblyState(id, AssemblyProposalStatus.Approved, DateTime.UtcNow, "Approved by admin");

        state.ProposalId.Should().Be(id);
        state.Status.Should().Be(AssemblyProposalStatus.Approved);
        state.Details.Should().Be("Approved by admin");
    }

    [Fact]
    public void CapabilityGap_ShouldSetProperties()
    {
        var gap = new CapabilityGap
        {
            Description = "Missing web scraping",
            Rationale = "Need to access web content",
            Importance = 0.8,
            AffectedTopics = new[] { "research", "web" },
            SuggestedCapabilities = new[] { NeuronCapability.ApiIntegration },
            IdentifiedBy = "gap-analyzer"
        };

        gap.Description.Should().Be("Missing web scraping");
        gap.Importance.Should().Be(0.8);
        gap.AffectedTopics.Should().HaveCount(2);
    }

    [Fact]
    public void MeTTaValidation_ShouldSetProperties()
    {
        var validation = new MeTTaValidation(true, 0.95, new List<string>(), new List<string> { "minor warning" }, "(= test True)");

        validation.IsValid.Should().BeTrue();
        validation.SafetyScore.Should().Be(0.95);
        validation.Violations.Should().BeEmpty();
        validation.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void NeuronBlueprint_ShouldSetProperties()
    {
        var blueprint = new NeuronBlueprint
        {
            Name = "ReflectionNeuron",
            Description = "Handles reflection",
            Rationale = "Needed for self-improvement",
            SubscribedTopics = new[] { "reflection.*" },
            ConfidenceScore = 0.9
        };

        blueprint.Name.Should().Be("ReflectionNeuron");
        blueprint.HasAutonomousTick.Should().BeFalse();
        blueprint.ConfidenceScore.Should().Be(0.9);
    }
}
