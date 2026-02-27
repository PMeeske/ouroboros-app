// <copyright file="OpenClawResilienceConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawResilienceConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        var config = new OpenClawResilienceConfig();

        config.RpcTimeout.Should().Be(TimeSpan.FromSeconds(15));
        config.RpcMaxRetries.Should().Be(3);
        config.RpcRetryBaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        config.CircuitBreakerFailureRatio.Should().Be(0.5);
        config.CircuitBreakerMinThroughput.Should().Be(5);
        config.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(10));
        config.ConnectMaxRetries.Should().Be(5);
        config.ReconnectMaxRetries.Should().Be(10);
        config.ReconnectMaxDelay.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void CreateDevelopment_HasShorterTimeouts()
    {
        var config = OpenClawResilienceConfig.CreateDevelopment();

        config.RpcTimeout.Should().Be(TimeSpan.FromSeconds(5));
        config.RpcMaxRetries.Should().Be(2);
        config.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(5));
        config.ConnectMaxRetries.Should().Be(3);
        config.ReconnectMaxRetries.Should().Be(5);
    }

    [Fact]
    public void CreateProduction_MatchesDefaults()
    {
        var config = OpenClawResilienceConfig.CreateProduction();
        var defaults = new OpenClawResilienceConfig();

        config.RpcTimeout.Should().Be(defaults.RpcTimeout);
        config.RpcMaxRetries.Should().Be(defaults.RpcMaxRetries);
        config.ConnectMaxRetries.Should().Be(defaults.ConnectMaxRetries);
    }

    [Fact]
    public void CreateAlwaysOn_HasAggressiveRetries()
    {
        var config = OpenClawResilienceConfig.CreateAlwaysOn();

        config.RpcMaxRetries.Should().Be(5);
        config.ConnectMaxRetries.Should().Be(8);
        config.ReconnectMaxRetries.Should().Be(int.MaxValue);
        config.ReconnectMaxDelay.Should().Be(TimeSpan.FromSeconds(300));
        config.ReconnectCircuitBreakerFailureRatio.Should().Be(0.95);
    }

    [Fact]
    public void CustomConfig_InitProperties_AreHonored()
    {
        var config = new OpenClawResilienceConfig
        {
            RpcTimeout = TimeSpan.FromSeconds(30),
            RpcMaxRetries = 10,
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(60),
        };

        config.RpcTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.RpcMaxRetries.Should().Be(10);
        config.CircuitBreakerBreakDuration.Should().Be(TimeSpan.FromSeconds(60));
    }
}
