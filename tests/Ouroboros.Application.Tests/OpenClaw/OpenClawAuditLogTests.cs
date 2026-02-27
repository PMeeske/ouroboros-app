// <copyright file="OpenClawAuditLogTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class OpenClawAuditLogTests
{
    [Fact]
    public void Constructor_DefaultMaxEntries_Is1000()
    {
        var log = new OpenClawAuditLog();
        log.MaxEntries.Should().Be(1000);
    }

    [Fact]
    public void Constructor_NegativeMaxEntries_DefaultsTo1000()
    {
        var log = new OpenClawAuditLog(-5);
        log.MaxEntries.Should().Be(1000);
    }

    [Fact]
    public void Constructor_ZeroMaxEntries_DefaultsTo1000()
    {
        var log = new OpenClawAuditLog(0);
        log.MaxEntries.Should().Be(1000);
    }

    [Fact]
    public void Constructor_CustomMaxEntries_IsHonored()
    {
        var log = new OpenClawAuditLog(50);
        log.MaxEntries.Should().Be(50);
    }

    [Fact]
    public void LogAllowed_IncrementsTotalAllowed()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "channel", "target123456", "detail");

        log.TotalAllowed.Should().Be(1);
        log.TotalDenied.Should().Be(0);
        log.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void LogDenied_IncrementsTotalDenied()
    {
        var log = new OpenClawAuditLog();
        log.LogDenied("tool", "channel", "target123456", "reason");

        log.TotalDenied.Should().Be(1);
        log.TotalAllowed.Should().Be(0);
        log.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void GetEntries_ReturnsNewestFirst()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool1", "ch", "target123456", "first");
        log.LogAllowed("tool2", "ch", "target123456", "second");
        log.LogDenied("tool3", "ch", "target123456", "third");

        var entries = log.GetEntries();
        entries.Should().HaveCount(3);
        entries[0].ToolName.Should().Be("tool3");
        entries[1].ToolName.Should().Be("tool2");
        entries[2].ToolName.Should().Be("tool1");
    }

    [Fact]
    public void LogAllowed_SetsVerdictToAllowed()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "target123456", "detail");

        var entry = log.GetEntries()[0];
        entry.Verdict.Should().Be(AuditVerdict.Allowed);
        entry.DenyReason.Should().BeNull();
        entry.Detail.Should().Be("detail");
    }

    [Fact]
    public void LogDenied_SetsVerdictToDenied()
    {
        var log = new OpenClawAuditLog();
        log.LogDenied("tool", "ch", "target123456", "blocked");

        var entry = log.GetEntries()[0];
        entry.Verdict.Should().Be(AuditVerdict.Denied);
        entry.DenyReason.Should().Be("blocked");
        entry.Detail.Should().BeNull();
    }

    [Fact]
    public void Eviction_DropsOldestWhenOverCapacity()
    {
        var log = new OpenClawAuditLog(3);
        log.LogAllowed("t1", "ch", "target123456", "d1");
        log.LogAllowed("t2", "ch", "target123456", "d2");
        log.LogAllowed("t3", "ch", "target123456", "d3");
        log.LogAllowed("t4", "ch", "target123456", "d4");

        var entries = log.GetEntries();
        entries.Should().HaveCount(3);
        entries.Select(e => e.ToolName).Should().NotContain("t1");
        entries.Select(e => e.ToolName).Should().Contain("t4");
    }

    [Fact]
    public void TotalOperations_IncludesEvictedEntries()
    {
        var log = new OpenClawAuditLog(2);
        log.LogAllowed("t1", "ch", "target123456", "d");
        log.LogAllowed("t2", "ch", "target123456", "d");
        log.LogDenied("t3", "ch", "target123456", "r");

        log.TotalOperations.Should().Be(3);
        log.TotalAllowed.Should().Be(2);
        log.TotalDenied.Should().Be(1);
    }

    [Fact]
    public void GetSummary_ContainsTotals()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "target123456", "d");
        log.LogDenied("tool", "ch", "target123456", "r");

        var summary = log.GetSummary();
        summary.Should().Contain("1 allowed");
        summary.Should().Contain("1 denied");
    }

    [Fact]
    public void GetSummary_IncludesRecentEntries()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("myTool", "ch", "target123456", "d");

        var summary = log.GetSummary();
        summary.Should().Contain("Recent:");
        summary.Should().Contain("myTool");
    }

    [Fact]
    public void MaskTarget_ShortTarget_ReturnsMasked()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "abc", "d");

        var entry = log.GetEntries()[0];
        entry.Target.Should().Be("***");
    }

    [Fact]
    public void MaskTarget_PhoneNumber_MasksMiddle()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "+15551234567", "d");

        var entry = log.GetEntries()[0];
        entry.Target.Should().StartWith("+155");
        entry.Target.Should().EndWith("4567");
        entry.Target.Should().Contain("*");
    }

    [Fact]
    public void MaskTarget_Email_MasksLocalPart()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "user@example.com", "d");

        var entry = log.GetEntries()[0];
        entry.Target.Should().StartWith("u");
        entry.Target.Should().Contain("***");
        entry.Target.Should().Contain("@example.com");
    }

    [Fact]
    public void MaskTarget_GenericLongTarget_MasksMiddle()
    {
        var log = new OpenClawAuditLog();
        log.LogAllowed("tool", "ch", "abcdefghij", "d");

        var entry = log.GetEntries()[0];
        entry.Target.Should().StartWith("abc");
        entry.Target.Should().EndWith("ij");
        entry.Target.Should().Contain("***");
    }
}
