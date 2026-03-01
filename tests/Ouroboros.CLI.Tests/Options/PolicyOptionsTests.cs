using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class PolicyOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new PolicyOptions { Command = "list" };

        options.Command.Should().Be("list");
        options.Name.Should().BeNull();
        options.Description.Should().BeNull();
        options.PolicyId.Should().BeNull();
        options.FilePath.Should().BeNull();
        options.OutputPath.Should().BeNull();
        options.Format.Should().Be("summary");
        options.Limit.Should().Be(50);
        options.Since.Should().BeNull();
        options.ApprovalId.Should().BeNull();
        options.Decision.Should().BeNull();
        options.ApproverId.Should().BeNull();
        options.Comments.Should().BeNull();
        options.Culture.Should().BeNull();
        options.EnableSelfModification.Should().BeFalse();
        options.RiskLevel.Should().Be("Medium");
        options.AutoApproveLow.Should().BeTrue();
        options.Verbose.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new PolicyOptions
        {
            Command = "create",
            Name = "safety-policy",
            Description = "Safety policy for testing",
            PolicyId = "pol-123",
            FilePath = "/tmp/policy.json",
            OutputPath = "/tmp/audit.json",
            Format = "json",
            Limit = 10,
            Since = "2024-01-01",
            ApprovalId = "apr-456",
            Decision = "approve",
            ApproverId = "admin",
            Comments = "Approved after review",
            Culture = "en-US",
            EnableSelfModification = true,
            RiskLevel = "High",
            AutoApproveLow = false,
            Verbose = true
        };

        options.Command.Should().Be("create");
        options.Name.Should().Be("safety-policy");
        options.Description.Should().Be("Safety policy for testing");
        options.PolicyId.Should().Be("pol-123");
        options.FilePath.Should().Be("/tmp/policy.json");
        options.OutputPath.Should().Be("/tmp/audit.json");
        options.Format.Should().Be("json");
        options.Limit.Should().Be(10);
        options.Since.Should().Be("2024-01-01");
        options.ApprovalId.Should().Be("apr-456");
        options.Decision.Should().Be("approve");
        options.ApproverId.Should().Be("admin");
        options.Comments.Should().Be("Approved after review");
        options.Culture.Should().Be("en-US");
        options.EnableSelfModification.Should().BeTrue();
        options.RiskLevel.Should().Be("High");
        options.AutoApproveLow.Should().BeFalse();
        options.Verbose.Should().BeTrue();
    }
}
