// <copyright file="OpenClawPcNodeToolsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests.Tools;

/// <summary>
/// Tests for OpenClawPcNodeTools static class and its tool implementations.
/// Tests focus on: tool naming, not-running behavior, and tool collection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawPcNodeToolsTests : IDisposable
{
    public OpenClawPcNodeToolsTests()
    {
        // Reset shared state
        OpenClawPcNodeTools.SharedPcNode = null;
        OpenClawPcNodeTools.SharedEventBus = null;
    }

    public void Dispose()
    {
        OpenClawPcNodeTools.SharedPcNode = null;
        OpenClawPcNodeTools.SharedEventBus = null;
    }

    // ── GetAllTools ────────────────────────────────────────────────────

    [Fact]
    public void GetAllTools_ReturnsFourTools()
    {
        var tools = OpenClawPcNodeTools.GetAllTools().ToList();
        tools.Should().HaveCount(4);
    }

    [Fact]
    public void GetAllTools_AllHaveUniqueNames()
    {
        var tools = OpenClawPcNodeTools.GetAllTools().ToList();
        var names = tools.Select(t => t.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAllTools_AllHaveDescriptions()
    {
        var tools = OpenClawPcNodeTools.GetAllTools().ToList();
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description));
    }

    // ── Tool Naming ────────────────────────────────────────────────────

    [Fact]
    public void PcCapabilitiesTool_HasCorrectName()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcCapabilitiesTool();
        tool.Name.Should().Be("openclaw_pc_capabilities");
    }

    [Fact]
    public void PcToggleCapabilityTool_HasCorrectName()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcToggleCapabilityTool();
        tool.Name.Should().Be("openclaw_pc_toggle_capability");
    }

    [Fact]
    public void ApprovalListTool_HasCorrectName()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalListTool();
        tool.Name.Should().Be("openclaw_approval_list");
    }

    [Fact]
    public void ApprovalRespondTool_HasCorrectName()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalRespondTool();
        tool.Name.Should().Be("openclaw_approval_respond");
    }

    // ── Not Running Behavior ───────────────────────────────────────────

    [Fact]
    public async Task PcCapabilitiesTool_WhenNotRunning_ReturnsFailure()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcCapabilitiesTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not running");
    }

    [Fact]
    public async Task PcToggleCapabilityTool_WhenNotRunning_ReturnsFailure()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcToggleCapabilityTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not running");
    }

    [Fact]
    public async Task ApprovalListTool_WhenNotRunning_ReturnsFailure()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalListTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not running");
    }

    [Fact]
    public async Task ApprovalRespondTool_WhenNotRunning_ReturnsFailure()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalRespondTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not running");
    }

    // ── JSON Schema ────────────────────────────────────────────────────

    [Fact]
    public void PcCapabilitiesTool_HasNoSchema()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcCapabilitiesTool();
        tool.JsonSchema.Should().BeNull();
    }

    [Fact]
    public void PcToggleCapabilityTool_HasSchema()
    {
        var tool = new OpenClawPcNodeTools.OpenClawPcToggleCapabilityTool();
        tool.JsonSchema.Should().NotBeNull();
        tool.JsonSchema.Should().Contain("capability");
        tool.JsonSchema.Should().Contain("enabled");
    }

    [Fact]
    public void ApprovalListTool_HasNoSchema()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalListTool();
        tool.JsonSchema.Should().BeNull();
    }

    [Fact]
    public void ApprovalRespondTool_HasSchema()
    {
        var tool = new OpenClawPcNodeTools.OpenClawApprovalRespondTool();
        tool.JsonSchema.Should().NotBeNull();
        tool.JsonSchema.Should().Contain("requestId");
        tool.JsonSchema.Should().Contain("approved");
    }

    // ── PcToggleCapabilityTool JSON Error ──────────────────────────────

    [Fact]
    public async Task PcToggleCapabilityTool_InvalidJson_ReturnsFailure()
    {
        // Need to set shared node to pass the null check
        var config = PcNodeSecurityConfig.CreateDevelopment();
        var node = new OpenClawPcNode(config);
        OpenClawPcNodeTools.SharedPcNode = node;

        var tool = new OpenClawPcNodeTools.OpenClawPcToggleCapabilityTool();
        var result = await tool.InvokeAsync("not json", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ApprovalRespondTool_InvalidJson_ReturnsFailure()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        var node = new OpenClawPcNode(config);
        OpenClawPcNodeTools.SharedPcNode = node;

        var tool = new OpenClawPcNodeTools.OpenClawApprovalRespondTool();
        var result = await tool.InvokeAsync("not json", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON");
    }

    // ── With Running Node ──────────────────────────────────────────────

    [Fact]
    public async Task PcCapabilitiesTool_WhenRunning_ReturnsCapabilities()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        var node = new OpenClawPcNode(config);
        OpenClawPcNodeTools.SharedPcNode = node;

        var tool = new OpenClawPcNodeTools.OpenClawPcCapabilitiesTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("PC Node Capabilities");
    }

    [Fact]
    public async Task ApprovalListTool_WhenRunning_NoPending_ReturnsEmpty()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        var node = new OpenClawPcNode(config);
        OpenClawPcNodeTools.SharedPcNode = node;
        OpenClawPcNodeTools.PendingApprovals.Clear();

        var tool = new OpenClawPcNodeTools.OpenClawApprovalListTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("No pending");
    }
}
