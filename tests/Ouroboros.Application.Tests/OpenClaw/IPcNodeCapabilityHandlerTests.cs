// <copyright file="IPcNodeCapabilityHandlerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class IPcNodeCapabilityHandlerTests
{
    // ── PcNodeResult ───────────────────────────────────────────────────

    [Fact]
    public void PcNodeResult_Ok_WithElement_IsSuccess()
    {
        var json = JsonSerializer.SerializeToElement(new { value = 42 });
        var result = PcNodeResult.Ok(json);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Base64Payload.Should().BeNull();
    }

    [Fact]
    public void PcNodeResult_Ok_WithMessage_IsSuccess()
    {
        var result = PcNodeResult.Ok("done");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void PcNodeResult_OkWithPayload_HasBase64()
    {
        var result = PcNodeResult.OkWithPayload("aGVsbG8=", "screenshot");

        result.Success.Should().BeTrue();
        result.Base64Payload.Should().Be("aGVsbG8=");
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public void PcNodeResult_OkWithPayload_NullDescription_DataIsNull()
    {
        var result = PcNodeResult.OkWithPayload("aGVsbG8=");

        result.Success.Should().BeTrue();
        result.Base64Payload.Should().Be("aGVsbG8=");
        result.Data.Should().BeNull();
    }

    [Fact]
    public void PcNodeResult_Fail_IsFailure()
    {
        var result = PcNodeResult.Fail("something went wrong");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
        result.Data.Should().BeNull();
        result.Base64Payload.Should().BeNull();
    }

    // ── PcNodeExecutionContext ──────────────────────────────────────────

    [Fact]
    public void PcNodeExecutionContext_PropertiesSet()
    {
        var auditLog = new OpenClawAuditLog();
        var ctx = new PcNodeExecutionContext("req-1", "device-abc", DateTime.UtcNow, auditLog);

        ctx.RequestId.Should().Be("req-1");
        ctx.CallerDeviceId.Should().Be("device-abc");
        ctx.AuditLog.Should().BeSameAs(auditLog);
    }

    // ── CapabilityDescriptor ───────────────────────────────────────────

    [Fact]
    public void CapabilityDescriptor_PropertiesSet()
    {
        var desc = new CapabilityDescriptor("system.info", "Get system info", null);

        desc.Name.Should().Be("system.info");
        desc.Description.Should().Be("Get system info");
        desc.ParameterSchema.Should().BeNull();
    }

    [Fact]
    public void CapabilityDescriptor_WithSchema_PropertiesSet()
    {
        var desc = new CapabilityDescriptor("file.read", "Read file", "{}");

        desc.ParameterSchema.Should().Be("{}");
    }

    // ── PcNodeRiskLevel ────────────────────────────────────────────────

    [Fact]
    public void PcNodeRiskLevel_HasCorrectOrdering()
    {
        ((int)PcNodeRiskLevel.Low).Should().BeLessThan((int)PcNodeRiskLevel.Medium);
        ((int)PcNodeRiskLevel.Medium).Should().BeLessThan((int)PcNodeRiskLevel.High);
        ((int)PcNodeRiskLevel.High).Should().BeLessThan((int)PcNodeRiskLevel.Critical);
    }

    // ── OpenClawEvent ──────────────────────────────────────────────────

    [Fact]
    public void OpenClawEvent_PropertiesSet()
    {
        var payload = JsonSerializer.SerializeToElement(new { key = "val" });
        var ts = DateTime.UtcNow;
        var evt = new OpenClawEvent("test.event", payload, ts);

        evt.EventType.Should().Be("test.event");
        evt.Timestamp.Should().Be(ts);
    }
}
