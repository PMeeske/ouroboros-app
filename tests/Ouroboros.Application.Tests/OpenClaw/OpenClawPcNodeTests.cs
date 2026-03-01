// <copyright file="OpenClawPcNodeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawPcNodeTests
{
    private static PcNodeSecurityConfig CreateDevConfig() =>
        PcNodeSecurityConfig.CreateDevelopment();

    [Fact]
    public void Constructor_ValidConfig_Succeeds()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        node.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        var act = () => new OpenClawPcNode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsConnected_WhenNew_IsFalse()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        node.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Config_ReturnsProvidedConfig()
    {
        var config = CreateDevConfig();
        var node = new OpenClawPcNode(config);
        node.Config.Should().BeSameAs(config);
    }

    [Fact]
    public void Capabilities_IsNotNull()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        node.Capabilities.Should().NotBeNull();
    }

    [Fact]
    public void Resilience_IsNotNull()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        node.Resilience.Should().NotBeNull();
    }

    [Fact]
    public void GetCapabilityStatus_ReturnsCapabilities()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        var status = node.GetCapabilityStatus();

        status.Should().NotBeEmpty();
        status.Should().Contain(c => c.Name == "system.info");
    }

    [Fact]
    public void GetAuditSummary_ReturnsNonEmpty()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        var summary = node.GetAuditSummary();
        summary.Should().NotBeNullOrEmpty();
        summary.Should().Contain("OpenClaw Audit");
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        var act = async () => await node.DisconnectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        var act = async () => await node.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void OnApprovalRequired_CanBeSet()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        node.OnApprovalRequired = _ => Task.FromResult(true);
        node.OnApprovalRequired.Should().NotBeNull();
    }

    [Fact]
    public void OnEvent_CanSubscribeAndUnsubscribe()
    {
        var node = new OpenClawPcNode(CreateDevConfig());
        var invoked = false;
        Action<OpenClawEvent> handler = _ => { invoked = true; };

        node.OnEvent += handler;
        node.OnEvent -= handler;

        invoked.Should().BeFalse();
    }

    // ── ApprovalRequest Record ─────────────────────────────────────────

    [Fact]
    public void ApprovalRequest_PropertiesSet()
    {
        var req = new ApprovalRequest(
            "req-1", "device-abc", "system.run",
            "{}", PcNodeRiskLevel.Critical);

        req.RequestId.Should().Be("req-1");
        req.CallerDeviceId.Should().Be("device-abc");
        req.Capability.Should().Be("system.run");
        req.Parameters.Should().Be("{}");
        req.RiskLevel.Should().Be(PcNodeRiskLevel.Critical);
    }
}
