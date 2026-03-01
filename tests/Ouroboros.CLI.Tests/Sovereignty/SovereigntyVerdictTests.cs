using Ouroboros.CLI.Sovereignty;

namespace Ouroboros.Tests.CLI.Sovereignty;

[Trait("Category", "Unit")]
public class SovereigntyVerdictTests
{
    [Fact]
    public void Allow_CreatesApprovedVerdict()
    {
        var verdict = SovereigntyVerdict.Allow("Aligned with values");

        verdict.Approved.Should().BeTrue();
        verdict.Reason.Should().Be("Aligned with values");
        verdict.RawResponse.Should().BeEmpty();
    }

    [Fact]
    public void Allow_WithRawResponse_SetsRaw()
    {
        var verdict = SovereigntyVerdict.Allow("OK", "APPROVE: OK");

        verdict.Approved.Should().BeTrue();
        verdict.RawResponse.Should().Be("APPROVE: OK");
    }

    [Fact]
    public void Deny_CreatesRejectedVerdict()
    {
        var verdict = SovereigntyVerdict.Deny("Not aligned");

        verdict.Approved.Should().BeFalse();
        verdict.Reason.Should().Be("Not aligned");
        verdict.RawResponse.Should().BeEmpty();
    }

    [Fact]
    public void Deny_WithRawResponse_SetsRaw()
    {
        var verdict = SovereigntyVerdict.Deny("No", "REJECT: No");

        verdict.Approved.Should().BeFalse();
        verdict.RawResponse.Should().Be("REJECT: No");
    }

    [Fact]
    public void DenyOnError_CreatesRejectedVerdictWithErrorMessage()
    {
        var verdict = SovereigntyVerdict.DenyOnError("Connection timeout");

        verdict.Approved.Should().BeFalse();
        verdict.Reason.Should().Contain("Sovereignty gate error");
        verdict.Reason.Should().Contain("Connection timeout");
        verdict.RawResponse.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_DirectCreation_SetsAllProperties()
    {
        var verdict = new SovereigntyVerdict(true, "Reason", "Raw");

        verdict.Approved.Should().BeTrue();
        verdict.Reason.Should().Be("Reason");
        verdict.RawResponse.Should().Be("Raw");
    }

    [Fact]
    public void Equality_IdenticalVerdicts_AreEqual()
    {
        var v1 = SovereigntyVerdict.Allow("OK");
        var v2 = SovereigntyVerdict.Allow("OK");

        v1.Should().Be(v2);
    }

    [Fact]
    public void Equality_DifferentApproval_AreNotEqual()
    {
        var v1 = SovereigntyVerdict.Allow("OK");
        var v2 = SovereigntyVerdict.Deny("OK");

        v1.Should().NotBe(v2);
    }
}
