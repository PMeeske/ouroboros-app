// <copyright file="OpenClawToolsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests.Tools;

/// <summary>
/// Tests for OpenClawTools static class and its tool implementations.
/// Tests focus on: tool naming, JSON schema, not-connected behavior,
/// and policy-not-initialized behavior. Does not test actual gateway calls.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawToolsTests : IDisposable
{
    public OpenClawToolsTests()
    {
        // Reset shared state before each test
        OpenClawTools.SharedClient = null;
        OpenClawTools.SharedPolicy = null;
    }

    public void Dispose()
    {
        OpenClawTools.SharedClient = null;
        OpenClawTools.SharedPolicy = null;
    }

    // ── GetAllTools ────────────────────────────────────────────────────

    [Fact]
    public void GetAllTools_ReturnsExpectedToolCount()
    {
        var tools = OpenClawTools.GetAllTools().ToList();
        tools.Should().NotBeEmpty();
        tools.Count.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void GetAllTools_AllHaveUniqueNames()
    {
        var tools = OpenClawTools.GetAllTools().ToList();
        var names = tools.Select(t => t.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAllTools_AllHaveDescriptions()
    {
        var tools = OpenClawTools.GetAllTools().ToList();
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description));
    }

    // ── Tool Naming ────────────────────────────────────────────────────

    [Fact]
    public void StatusTool_HasCorrectName()
    {
        var tool = new OpenClawTools.OpenClawStatusTool();
        tool.Name.Should().Be("openclaw_status");
    }

    [Fact]
    public void ListChannelsTool_HasCorrectName()
    {
        var tool = new OpenClawTools.OpenClawListChannelsTool();
        tool.Name.Should().Be("openclaw_list_channels");
    }

    [Fact]
    public void NodeListTool_HasCorrectName()
    {
        var tool = new OpenClawTools.OpenClawNodeListTool();
        tool.Name.Should().Be("openclaw_node_list");
    }

    [Fact]
    public void SendMessageTool_HasCorrectName()
    {
        var tool = new OpenClawTools.OpenClawSendMessageTool();
        tool.Name.Should().Be("openclaw_send_message");
    }

    [Fact]
    public void NodeInvokeTool_HasCorrectName()
    {
        var tool = new OpenClawTools.OpenClawNodeInvokeTool();
        tool.Name.Should().Be("openclaw_node_invoke");
    }

    // ── Not Connected Behavior ─────────────────────────────────────────

    [Fact]
    public async Task StatusTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawStatusTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not connected");
    }

    [Fact]
    public async Task ListChannelsTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawListChannelsTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task NodeListTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawNodeListTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SendMessageTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawSendMessageTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task NodeInvokeTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawNodeInvokeTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SessionsListTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawSessionsListTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task MemorySearchTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawMemorySearchTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task MemoryGetTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawMemoryGetTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task CameraSnapTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawCameraSnapTool();
        var result = await tool.InvokeAsync("{}", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HealthTool_WhenNotConnected_ReturnsFailure()
    {
        var tool = new OpenClawTools.OpenClawHealthTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    // ── JSON Schema ────────────────────────────────────────────────────

    [Fact]
    public void SendMessageTool_HasJsonSchema()
    {
        var tool = new OpenClawTools.OpenClawSendMessageTool();
        tool.JsonSchema.Should().NotBeNull();
        tool.JsonSchema.Should().Contain("channel");
        tool.JsonSchema.Should().Contain("message");
    }

    [Fact]
    public void NodeInvokeTool_HasJsonSchema()
    {
        var tool = new OpenClawTools.OpenClawNodeInvokeTool();
        tool.JsonSchema.Should().NotBeNull();
        tool.JsonSchema.Should().Contain("node");
        tool.JsonSchema.Should().Contain("command");
    }

    [Fact]
    public void StatusTool_HasNoSchema()
    {
        var tool = new OpenClawTools.OpenClawStatusTool();
        tool.JsonSchema.Should().BeNull();
    }
}
