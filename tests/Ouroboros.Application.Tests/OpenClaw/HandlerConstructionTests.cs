// <copyright file="HandlerConstructionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.OpenClaw.PcNode.Handlers;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

/// <summary>
/// Tests handler construction, null guard checks, and capability names.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class HandlerConstructionTests
{
    private static PcNodeSecurityPolicy CreatePolicy()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        return new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
    }

    // ── Null Guard Tests ───────────────────────────────────────────────

    [Fact]
    public void ClipboardWriteHandler_NullConfig_Throws()
    {
        var act = () => new ClipboardWriteHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScreenCaptureHandler_NullConfig_Throws()
    {
        var act = () => new ScreenCaptureHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScreenRecordHandler_NullConfig_Throws()
    {
        var act = () => new ScreenRecordHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BrowserOpenHandler_NullPolicy_Throws()
    {
        var act = () => new BrowserOpenHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FileListHandler_NullPolicy_Throws()
    {
        var act = () => new FileListHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FileReadHandler_NullPolicy_Throws()
    {
        var act = () => new FileReadHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FileWriteHandler_NullPolicy_Throws()
    {
        var act = () => new FileWriteHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FileDeleteHandler_NullPolicy_Throws()
    {
        var act = () => new FileDeleteHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProcessKillHandler_NullPolicy_Throws()
    {
        var act = () => new ProcessKillHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AppLaunchHandler_NullPolicy_Throws()
    {
        var act = () => new AppLaunchHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShellCommandHandler_NullPolicy_Throws()
    {
        var act = () => new ShellCommandHandler(null!, PcNodeSecurityConfig.CreateDevelopment());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShellCommandHandler_NullConfig_Throws()
    {
        var act = () => new ShellCommandHandler(CreatePolicy(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Capability Name Tests ──────────────────────────────────────────

    [Fact]
    public void SystemInfoHandler_CapabilityName_IsSystemInfo()
    {
        new SystemInfoHandler().CapabilityName.Should().Be("system.info");
    }

    [Fact]
    public void SystemNotifyHandler_CapabilityName_IsSystemNotify()
    {
        new SystemNotifyHandler().CapabilityName.Should().Be("system.notify");
    }

    [Fact]
    public void ClipboardReadHandler_CapabilityName_IsClipboardRead()
    {
        new ClipboardReadHandler().CapabilityName.Should().Be("clipboard.read");
    }

    [Fact]
    public void ClipboardWriteHandler_CapabilityName_IsClipboardWrite()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        new ClipboardWriteHandler(config).CapabilityName.Should().Be("clipboard.write");
    }

    [Fact]
    public void ScreenCaptureHandler_CapabilityName_IsScreenCapture()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        new ScreenCaptureHandler(config).CapabilityName.Should().Be("screen.capture");
    }

    [Fact]
    public void ScreenRecordHandler_CapabilityName_IsScreenRecord()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        new ScreenRecordHandler(config).CapabilityName.Should().Be("screen.record");
    }

    [Fact]
    public void BrowserOpenHandler_CapabilityName_IsBrowserOpen()
    {
        new BrowserOpenHandler(CreatePolicy()).CapabilityName
            .Should().Be("browser.open");
    }

    [Fact]
    public void FileListHandler_CapabilityName_IsFileList()
    {
        new FileListHandler(CreatePolicy()).CapabilityName
            .Should().Be("file.list");
    }

    [Fact]
    public void FileReadHandler_CapabilityName_IsFileRead()
    {
        new FileReadHandler(CreatePolicy()).CapabilityName
            .Should().Be("file.read");
    }

    [Fact]
    public void FileWriteHandler_CapabilityName_IsFileWrite()
    {
        new FileWriteHandler(CreatePolicy()).CapabilityName
            .Should().Be("file.write");
    }

    [Fact]
    public void FileDeleteHandler_CapabilityName_IsFileDelete()
    {
        new FileDeleteHandler(CreatePolicy()).CapabilityName
            .Should().Be("file.delete");
    }

    [Fact]
    public void ProcessListHandler_CapabilityName_IsProcessList()
    {
        new ProcessListHandler().CapabilityName.Should().Be("process.list");
    }

    [Fact]
    public void ProcessKillHandler_CapabilityName_IsProcessKill()
    {
        new ProcessKillHandler(CreatePolicy()).CapabilityName
            .Should().Be("process.kill");
    }

    [Fact]
    public void AppLaunchHandler_CapabilityName_IsAppLaunch()
    {
        new AppLaunchHandler(CreatePolicy()).CapabilityName
            .Should().Be("app.launch");
    }

    [Fact]
    public void ShellCommandHandler_CapabilityName_IsSystemRun()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        new ShellCommandHandler(CreatePolicy(), config).CapabilityName
            .Should().Be("system.run");
    }
}
