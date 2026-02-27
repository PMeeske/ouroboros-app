// <copyright file="HandlerInterfaceComplianceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.OpenClaw.PcNode.Handlers;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

/// <summary>
/// Validates that every handler implements IPcNodeCapabilityHandler correctly:
/// non-null names, descriptions, and correct risk levels.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class HandlerInterfaceComplianceTests
{
    private static PcNodeSecurityPolicy CreatePolicy()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        return new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
    }

    private static PcNodeSecurityConfig CreateConfig() =>
        PcNodeSecurityConfig.CreateDevelopment();

    public static IEnumerable<object[]> AllHandlers()
    {
        var policy = CreatePolicy();
        var config = CreateConfig();

        yield return new object[] { new SystemInfoHandler() };
        yield return new object[] { new SystemNotifyHandler() };
        yield return new object[] { new ClipboardReadHandler() };
        yield return new object[] { new ClipboardWriteHandler(config) };
        yield return new object[] { new ScreenCaptureHandler(config) };
        yield return new object[] { new ScreenRecordHandler(config) };
        yield return new object[] { new BrowserOpenHandler(policy) };
        yield return new object[] { new FileListHandler(policy) };
        yield return new object[] { new FileReadHandler(policy) };
        yield return new object[] { new FileWriteHandler(policy) };
        yield return new object[] { new FileDeleteHandler(policy) };
        yield return new object[] { new ProcessListHandler() };
        yield return new object[] { new ProcessKillHandler(policy) };
        yield return new object[] { new AppLaunchHandler(policy) };
        yield return new object[] { new ShellCommandHandler(policy, config) };
    }

    [Theory]
    [MemberData(nameof(AllHandlers))]
    public void Handler_HasNonEmptyCapabilityName(IPcNodeCapabilityHandler handler)
    {
        handler.CapabilityName.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllHandlers))]
    public void Handler_HasNonEmptyDescription(IPcNodeCapabilityHandler handler)
    {
        handler.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllHandlers))]
    public void Handler_RiskLevel_IsValidEnum(IPcNodeCapabilityHandler handler)
    {
        handler.RiskLevel.Should().BeOneOf(
            PcNodeRiskLevel.Low, PcNodeRiskLevel.Medium,
            PcNodeRiskLevel.High, PcNodeRiskLevel.Critical);
    }

    // ── Specific Risk Level Assertions ─────────────────────────────────

    [Fact]
    public void SystemInfoHandler_IsLowRisk()
    {
        new SystemInfoHandler().RiskLevel.Should().Be(PcNodeRiskLevel.Low);
    }

    [Fact]
    public void SystemNotifyHandler_IsLowRisk()
    {
        new SystemNotifyHandler().RiskLevel.Should().Be(PcNodeRiskLevel.Low);
    }

    [Fact]
    public void ClipboardReadHandler_IsLowRisk()
    {
        new ClipboardReadHandler().RiskLevel.Should().Be(PcNodeRiskLevel.Low);
    }

    [Fact]
    public void ScreenCaptureHandler_IsMediumRisk()
    {
        new ScreenCaptureHandler(CreateConfig()).RiskLevel
            .Should().Be(PcNodeRiskLevel.Medium);
    }

    [Fact]
    public void FileDeleteHandler_IsHighRisk()
    {
        new FileDeleteHandler(CreatePolicy()).RiskLevel
            .Should().Be(PcNodeRiskLevel.High);
    }

    [Fact]
    public void ShellCommandHandler_IsCriticalRisk()
    {
        new ShellCommandHandler(CreatePolicy(), CreateConfig()).RiskLevel
            .Should().Be(PcNodeRiskLevel.Critical);
    }

    [Fact]
    public void ScreenRecordHandler_RequiresApproval()
    {
        new ScreenRecordHandler(CreateConfig()).RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void FileDeleteHandler_RequiresApproval()
    {
        new FileDeleteHandler(CreatePolicy()).RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void ShellCommandHandler_RequiresApproval()
    {
        new ShellCommandHandler(CreatePolicy(), CreateConfig())
            .RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void SystemInfoHandler_DoesNotRequireApproval()
    {
        new SystemInfoHandler().RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void ProcessListHandler_DoesNotRequireApproval()
    {
        new ProcessListHandler().RequiresApproval.Should().BeFalse();
    }
}
