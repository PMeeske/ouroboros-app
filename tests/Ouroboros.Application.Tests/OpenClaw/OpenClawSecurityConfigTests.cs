// <copyright file="OpenClawSecurityConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawSecurityConfigTests
{
    [Fact]
    public void CreateDefault_HasEmptyAllowlists()
    {
        var config = OpenClawSecurityConfig.CreateDefault();

        config.AllowedChannels.Should().BeEmpty();
        config.AllowedNodeCommands.Should().BeEmpty();
        config.AllowedRecipients.Should().BeEmpty();
    }

    [Fact]
    public void CreateDefault_HasSensitiveDataScanEnabled()
    {
        var config = OpenClawSecurityConfig.CreateDefault();

        config.EnableSensitiveDataScan.Should().BeTrue();
        config.EnableAuditLog.Should().BeTrue();
    }

    [Fact]
    public void CreateDefault_HasStandardLimits()
    {
        var config = OpenClawSecurityConfig.CreateDefault();

        config.MaxMessageLength.Should().Be(4096);
        config.GlobalRateLimitPerWindow.Should().Be(20);
        config.ChannelRateLimitPerWindow.Should().Be(10);
        config.RateLimitWindowSeconds.Should().Be(60);
        config.MaxAuditLogEntries.Should().Be(1000);
    }

    [Fact]
    public void CreateDevelopment_HasChannelsEnabled()
    {
        var config = OpenClawSecurityConfig.CreateDevelopment();

        config.AllowedChannels.Should().NotBeEmpty();
        config.AllowedChannels.Should().Contain("whatsapp");
        config.AllowedChannels.Should().Contain("telegram");
        config.AllowedChannels.Should().Contain("slack");
    }

    [Fact]
    public void CreateDevelopment_HasNodeCommandsEnabled()
    {
        var config = OpenClawSecurityConfig.CreateDevelopment();

        config.AllowedNodeCommands.Should().NotBeEmpty();
        config.AllowedNodeCommands.Should().Contain("camera.*");
        config.AllowedNodeCommands.Should().Contain("location.get");
    }

    [Fact]
    public void CreateDevelopment_HasHigherRateLimits()
    {
        var config = OpenClawSecurityConfig.CreateDevelopment();

        config.GlobalRateLimitPerWindow.Should().Be(60);
        config.ChannelRateLimitPerWindow.Should().Be(30);
    }

    [Fact]
    public void AllowedChannels_CaseInsensitive()
    {
        var config = new OpenClawSecurityConfig();
        config.AllowedChannels.Add("WhatsApp");

        config.AllowedChannels.Contains("whatsapp").Should().BeTrue();
        config.AllowedChannels.Contains("WHATSAPP").Should().BeTrue();
    }

    [Fact]
    public void AllowedNodeCommands_CaseInsensitive()
    {
        var config = new OpenClawSecurityConfig();
        config.AllowedNodeCommands.Add("Camera.*");

        config.AllowedNodeCommands.Contains("camera.*").Should().BeTrue();
    }
}
