// <copyright file="OpenClawTokenManagerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawTokenManagerTests
{
    [Fact]
    public void ResolveToken_ExplicitToken_ReturnsExplicit()
    {
        var token = OpenClawTokenManager.ResolveToken("my-explicit-token");
        token.Should().Be("my-explicit-token");
    }

    [Fact]
    public void ResolveToken_NullExplicit_DoesNotThrow()
    {
        // Validates the fallback chain works without throwing.
        // Result depends on env vars and config files present.
        var act = () => OpenClawTokenManager.ResolveToken(null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ResolveToken_WhitespaceExplicit_FallsThrough()
    {
        var token = OpenClawTokenManager.ResolveToken("   ");
        // Whitespace-only is treated as empty, should fall through
        token.Should().NotBe("   ");
    }

    [Fact]
    public void ResolveToken_EmptyExplicit_FallsThrough()
    {
        var token = OpenClawTokenManager.ResolveToken("");
        token.Should().NotBe("");
    }

    // ── ResolveGatewayUrl ──────────────────────────────────────────────

    [Fact]
    public void ResolveGatewayUrl_ExplicitUrl_ReturnsExplicit()
    {
        var url = OpenClawTokenManager.ResolveGatewayUrl("ws://custom:9999");
        url.Should().Be("ws://custom:9999");
    }

    [Fact]
    public void ResolveGatewayUrl_NullExplicit_ReturnsDefault()
    {
        var url = OpenClawTokenManager.ResolveGatewayUrl(null);
        // Should return either env var or default
        url.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ResolveGatewayUrl_NoArgs_ReturnsLocalhost()
    {
        // Without env var OPENCLAW_GATEWAY, should return default
        var url = OpenClawTokenManager.ResolveGatewayUrl();
        url.Should().Contain("127.0.0.1");
    }
}
