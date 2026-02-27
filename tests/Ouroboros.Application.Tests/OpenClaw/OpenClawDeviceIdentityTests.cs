// <copyright file="OpenClawDeviceIdentityTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawDeviceIdentityTests
{
    [Fact]
    public async Task LoadOrCreateAsync_GeneratesValidIdentity()
    {
        // This will create or load a device identity from disk
        var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();

        identity.Should().NotBeNull();
        identity.DeviceId.Should().NotBeNullOrEmpty();
        identity.DeviceId.Should().HaveLength(64); // SHA-256 hex = 64 chars
        identity.PublicKeyBase64Url.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadOrCreateAsync_ReturnsSameIdentityOnSubsequentCalls()
    {
        var id1 = await OpenClawDeviceIdentity.LoadOrCreateAsync();
        var id2 = await OpenClawDeviceIdentity.LoadOrCreateAsync();

        id1.DeviceId.Should().Be(id2.DeviceId);
        id1.PublicKeyBase64Url.Should().Be(id2.PublicKeyBase64Url);
    }

    [Fact]
    public async Task SignHandshake_ReturnsValidSignature()
    {
        var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
        var (signature, signedAt, nonce) = identity.SignHandshake(
            nonce: "test-nonce-123",
            clientId: "test-client",
            clientMode: "backend",
            role: "operator",
            scopesCsv: "operator.read,operator.write",
            tokenOrEmpty: "");

        signature.Should().NotBeNullOrEmpty();
        signedAt.Should().BeGreaterThan(0);
        nonce.Should().Be("test-nonce-123");
    }

    [Fact]
    public async Task SignHandshake_DifferentNonces_ProduceDifferentSignatures()
    {
        var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();

        var (sig1, _, _) = identity.SignHandshake("nonce1", "c", "b", "r", "s", "");
        var (sig2, _, _) = identity.SignHandshake("nonce2", "c", "b", "r", "s", "");

        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public async Task DeviceToken_IsAccessible()
    {
        var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();
        // DeviceToken may or may not be set depending on previous runs,
        // but the property should always be accessible (nullable string).
        var token = identity.DeviceToken;
        (token is null or { Length: > 0 }).Should().BeTrue(
            "DeviceToken should be null or a non-empty string");
    }

    [Fact]
    public async Task PublicKeyBase64Url_IsBase64UrlEncoded()
    {
        var identity = await OpenClawDeviceIdentity.LoadOrCreateAsync();

        // Base64Url should not contain +, /, or =
        identity.PublicKeyBase64Url.Should().NotContain("+");
        identity.PublicKeyBase64Url.Should().NotContain("/");
        identity.PublicKeyBase64Url.Should().NotEndWith("=");
    }
}
