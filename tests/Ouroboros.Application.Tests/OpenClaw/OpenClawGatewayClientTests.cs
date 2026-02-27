// <copyright file="OpenClawGatewayClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawGatewayClientTests
{
    [Fact]
    public void Constructor_NullIdentity_DoesNotThrow()
    {
        var client = new OpenClawGatewayClient(null, null, null);
        client.Should().NotBeNull();
    }

    [Fact]
    public void IsConnected_WhenNew_IsFalse()
    {
        var client = new OpenClawGatewayClient();
        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void IsReconnecting_WhenNew_IsFalse()
    {
        var client = new OpenClawGatewayClient();
        client.IsReconnecting.Should().BeFalse();
    }

    [Fact]
    public void LastReconnectError_WhenNew_IsNull()
    {
        var client = new OpenClawGatewayClient();
        client.LastReconnectError.Should().BeNull();
    }

    [Fact]
    public void Resilience_IsNotNull()
    {
        var client = new OpenClawGatewayClient();
        client.Resilience.Should().NotBeNull();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var client = new OpenClawGatewayClient();
        var act = async () => await client.DisconnectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var client = new OpenClawGatewayClient();
        var act = async () => await client.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void OnReconnectionFailed_CanSubscribeAndUnsubscribe()
    {
        var client = new OpenClawGatewayClient();
        var subscribed = false;
        Action<Exception?> handler = _ => { subscribed = true; };

        client.OnReconnectionFailed += handler;
        client.OnReconnectionFailed -= handler;

        // After unsubscribe, the handler should not be invoked
        subscribed.Should().BeFalse();
    }

    // ── OpenClawException ──────────────────────────────────────────────

    [Fact]
    public void OpenClawException_MessageOnly_Constructor()
    {
        var ex = new OpenClawException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void OpenClawException_WithInner_Constructor()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new OpenClawException("outer", inner);
        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
