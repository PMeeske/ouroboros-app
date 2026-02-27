// <copyright file="OpenClawSecurityPolicyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawSecurityPolicyTests
{
    private static OpenClawSecurityPolicy CreatePolicy(Action<OpenClawSecurityConfig>? configure = null)
    {
        var config = OpenClawSecurityConfig.CreateDevelopment();
        configure?.Invoke(config);
        return new OpenClawSecurityPolicy(config, new OpenClawAuditLog());
    }

    // ── Constructor ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        var act = () => new OpenClawSecurityPolicy(null!, new OpenClawAuditLog());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAuditLog_Throws()
    {
        var act = () => new OpenClawSecurityPolicy(new OpenClawSecurityConfig(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── ValidateSendMessage ────────────────────────────────────────────

    [Fact]
    public void ValidateSendMessage_AllowedChannel_Succeeds()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", "Hello");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateSendMessage_DisallowedChannel_Denied()
    {
        var policy = CreatePolicy(c => c.AllowedChannels.Clear());
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", "Hello");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not in the allowlist");
    }

    [Fact]
    public void ValidateSendMessage_DisallowedRecipient_Denied()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedRecipients["whatsapp"] = new HashSet<string> { "+15550000000" };
        });

        var verdict = policy.ValidateSendMessage("whatsapp", "+15559999999", "Hello");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not in the allowlist");
    }

    [Fact]
    public void ValidateSendMessage_AllowedRecipient_Succeeds()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedRecipients["whatsapp"] = new HashSet<string> { "+15551234567" };
        });

        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", "Hello");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateSendMessage_NoRecipientRestriction_AllowsAll()
    {
        var policy = CreatePolicy();
        // No AllowedRecipients configured for whatsapp = allow any
        var verdict = policy.ValidateSendMessage("whatsapp", "anyone@example.com", "Hello");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateSendMessage_ExceedsMaxLength_Denied()
    {
        var policy = CreatePolicy(c => c.MaxMessageLength = 10);
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", new string('x', 11));
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("exceeds maximum");
    }

    [Fact]
    public void ValidateSendMessage_ContainsSensitiveData_Denied()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567",
            "Here is the api_key=sk-ABCDEFGHIJKLMNOPQRSTuvwxyz1234567890");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("sensitive data");
    }

    [Fact]
    public void ValidateSendMessage_ContainsJwt_Denied()
    {
        var policy = CreatePolicy();
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", jwt);
        verdict.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateSendMessage_ContainsCreditCard_Denied()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567",
            "My card is 4111111111111111");
        verdict.IsAllowed.Should().BeFalse();
    }

    // ── ValidateNodeInvoke ─────────────────────────────────────────────

    [Fact]
    public void ValidateNodeInvoke_AllowedWildcard_Succeeds()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateNodeInvoke("node1", "camera.snap", null);
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateNodeInvoke_AllowedExact_Succeeds()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateNodeInvoke("node1", "location.get", null);
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateNodeInvoke_DisallowedCommand_Denied()
    {
        var policy = CreatePolicy(c => c.AllowedNodeCommands.Clear());
        var verdict = policy.ValidateNodeInvoke("node1", "camera.snap", null);
        verdict.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateNodeInvoke_DangerousCommand_Denied()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedNodeCommands.Add("system.run");
        });

        var verdict = policy.ValidateNodeInvoke("node1", "system.run", null);
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("dangerous");
    }

    // ── Rate Limiting ──────────────────────────────────────────────────

    [Fact]
    public void ValidateSendMessage_GlobalRateLimit_Enforced()
    {
        var policy = CreatePolicy(c => c.GlobalRateLimitPerWindow = 2);

        policy.ValidateSendMessage("whatsapp", "+15551234567", "m1").IsAllowed.Should().BeTrue();
        policy.ValidateSendMessage("telegram", "+15551234567", "m2").IsAllowed.Should().BeTrue();
        var verdict = policy.ValidateSendMessage("slack", "+15551234567", "m3");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("Rate limit");
    }

    [Fact]
    public void ValidateSendMessage_ChannelRateLimit_Enforced()
    {
        var policy = CreatePolicy(c =>
        {
            c.GlobalRateLimitPerWindow = 100;
            c.ChannelRateLimitPerWindow = 2;
        });

        policy.ValidateSendMessage("whatsapp", "+15551234567", "m1").IsAllowed.Should().BeTrue();
        policy.ValidateSendMessage("whatsapp", "+15551234567", "m2").IsAllowed.Should().BeTrue();
        var verdict = policy.ValidateSendMessage("whatsapp", "+15551234567", "m3");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("Rate limit");
    }

    // ── PolicyVerdict ──────────────────────────────────────────────────

    [Fact]
    public void PolicyVerdict_Allow_IsAllowed()
    {
        var verdict = PolicyVerdict.Allow();
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void PolicyVerdict_Deny_HasReason()
    {
        var verdict = PolicyVerdict.Deny("bad request");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Be("bad request");
    }
}
