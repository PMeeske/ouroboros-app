// <copyright file="OpenClawResiliencePipelineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Polly.CircuitBreaker;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawResiliencePipelineTests
{
    [Fact]
    public void Constructor_DefaultConfig_Succeeds()
    {
        var pipeline = new OpenClawResiliencePipeline();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConfig_Succeeds()
    {
        var config = OpenClawResilienceConfig.CreateDevelopment();
        var pipeline = new OpenClawResiliencePipeline(config);
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void RpcCircuitState_InitiallyClosed()
    {
        var pipeline = new OpenClawResiliencePipeline();
        pipeline.RpcCircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void ReconnectCircuitState_InitiallyClosed()
    {
        var pipeline = new OpenClawResiliencePipeline();
        pipeline.ReconnectCircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void GetStatusSummary_ContainsCircuitStates()
    {
        var pipeline = new OpenClawResiliencePipeline();
        var summary = pipeline.GetStatusSummary();

        summary.Should().Contain("RPC circuit");
        summary.Should().Contain("Reconnect circuit");
        summary.Should().Contain("Closed");
    }

    [Fact]
    public async Task ExecuteRpcAsync_SuccessfulOperation_ReturnsResult()
    {
        var pipeline = new OpenClawResiliencePipeline();
        var result = await pipeline.ExecuteRpcAsync(async _ =>
        {
            await Task.Yield();
            return 42;
        });

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteRpcAsync_VoidOverload_Completes()
    {
        var pipeline = new OpenClawResiliencePipeline();
        bool executed = false;

        await pipeline.ExecuteRpcAsync(async _ =>
        {
            await Task.Yield();
            executed = true;
        });

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteConnectAsync_SuccessfulConnect_Completes()
    {
        var pipeline = new OpenClawResiliencePipeline();
        bool connected = false;

        await pipeline.ExecuteConnectAsync(async _ =>
        {
            await Task.Yield();
            connected = true;
        });

        connected.Should().BeTrue();
    }
}
