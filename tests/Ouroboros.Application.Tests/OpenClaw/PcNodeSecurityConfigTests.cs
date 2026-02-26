// <copyright file="PcNodeSecurityConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw.PcNode;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class PcNodeSecurityConfigTests
{
    // ── CreateDefault (fail-closed) ─────────────────────────────────────────

    [Fact]
    public void CreateDefault_HasNoEnabledCapabilities()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.EnabledCapabilities.Should().BeEmpty("default config is fail-closed");
    }

    [Fact]
    public void CreateDefault_HasNoAllowedCallers()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.AllowedCallerDeviceIds.Should().BeEmpty("default config denies all callers");
    }

    [Fact]
    public void CreateDefault_HasNoAllowedFileDirectories()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.AllowedFileDirectories.Should().BeEmpty("default config denies all file access");
    }

    [Fact]
    public void CreateDefault_HasNoAllowedApplications()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.AllowedApplications.Should().BeEmpty("default config denies all app launches");
    }

    [Fact]
    public void CreateDefault_ShellCommandsDisabled()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.EnableShellCommands.Should().BeFalse("shell commands are disabled by default");
    }

    [Fact]
    public void CreateDefault_HasBlockedFileExtensions()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.BlockedFileExtensions.Should().Contain(".exe");
        config.BlockedFileExtensions.Should().Contain(".bat");
        config.BlockedFileExtensions.Should().Contain(".ps1");
        config.BlockedFileExtensions.Should().Contain(".dll");
    }

    [Fact]
    public void CreateDefault_HasProtectedProcesses()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.ProtectedProcesses.Should().Contain("csrss");
        config.ProtectedProcesses.Should().Contain("lsass");
        config.ProtectedProcesses.Should().Contain("svchost");
        config.ProtectedProcesses.Should().Contain("explorer");
    }

    [Fact]
    public void CreateDefault_HasBlockedShellPatterns()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.BlockedShellPatterns.Should().NotBeEmpty();
        config.BlockedShellPatterns.Should().Contain(p => p.Contains("rm"));
        config.BlockedShellPatterns.Should().Contain(p => p.Contains("format"));
    }

    [Fact]
    public void CreateDefault_ScanOutboundResultsEnabled()
    {
        var config = PcNodeSecurityConfig.CreateDefault();
        config.ScanOutboundResults.Should().BeTrue("outbound scanning is enabled by default");
    }

    // ── CreateDevelopment ────────────────────────────────────────────────────

    [Fact]
    public void CreateDevelopment_HasSafeCapabilitiesEnabled()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();

        config.EnabledCapabilities.Should().Contain("system.info");
        config.EnabledCapabilities.Should().Contain("system.notify");
        config.EnabledCapabilities.Should().Contain("clipboard.read");
        config.EnabledCapabilities.Should().Contain("screen.capture");
        config.EnabledCapabilities.Should().Contain("process.list");
    }

    [Fact]
    public void CreateDevelopment_DoesNotEnableDangerousCapabilities()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();

        config.EnabledCapabilities.Should().NotContain("system.run");
        config.EnabledCapabilities.Should().NotContain("file.delete");
        config.EnabledCapabilities.Should().NotContain("file.write");
    }

    [Fact]
    public void CreateDevelopment_AllowsAnyCaller()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        config.AllowedCallerDeviceIds.Should().Contain("*");
    }

    [Fact]
    public void CreateDevelopment_HasHigherRateLimits()
    {
        var defaultConfig = PcNodeSecurityConfig.CreateDefault();
        var devConfig = PcNodeSecurityConfig.CreateDevelopment();

        devConfig.GlobalRateLimitPerMinute.Should().BeGreaterThan(defaultConfig.GlobalRateLimitPerMinute);
    }

    // ── CreateFromFile ──────────────────────────────────────────────────────

    [Fact]
    public void CreateFromFile_DeserializesJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
            {
                "EnabledCapabilities": ["system.info", "clipboard.read"],
                "AllowedCallerDeviceIds": ["device-123"],
                "GlobalRateLimitPerMinute": 100,
                "EnableShellCommands": false
            }
            """;
            File.WriteAllText(tempFile, json);

            var config = PcNodeSecurityConfig.CreateFromFile(tempFile);

            config.EnabledCapabilities.Should().HaveCount(2);
            config.EnabledCapabilities.Should().Contain("system.info");
            config.EnabledCapabilities.Should().Contain("clipboard.read");
            config.AllowedCallerDeviceIds.Should().Contain("device-123");
            config.GlobalRateLimitPerMinute.Should().Be(100);
            config.EnableShellCommands.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CreateFromFile_SupportsCommentsAndTrailingCommas()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
            {
                // This is a comment
                "EnabledCapabilities": ["system.info"],
                "GlobalRateLimitPerMinute": 50,
            }
            """;
            File.WriteAllText(tempFile, json);

            var config = PcNodeSecurityConfig.CreateFromFile(tempFile);
            config.EnabledCapabilities.Should().Contain("system.info");
            config.GlobalRateLimitPerMinute.Should().Be(50);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── Defaults ────────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_MaxFileSize_Is10MB()
    {
        var config = new PcNodeSecurityConfig();
        config.MaxFileSize.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public void Defaults_ShellCommandTimeout_Is30Seconds()
    {
        var config = new PcNodeSecurityConfig();
        config.ShellCommandTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Defaults_ApprovalThreshold_IsHigh()
    {
        var config = new PcNodeSecurityConfig();
        config.ApprovalThreshold.Should().Be(PcNodeRiskLevel.High);
    }

    [Fact]
    public void Defaults_MaxClipboardLength_Is10000()
    {
        var config = new PcNodeSecurityConfig();
        config.MaxClipboardLength.Should().Be(10_000);
    }

    [Fact]
    public void Defaults_AllowedUrlSchemes_HttpAndHttps()
    {
        var config = new PcNodeSecurityConfig();
        config.AllowedUrlSchemes.Should().Contain("http");
        config.AllowedUrlSchemes.Should().Contain("https");
        config.AllowedUrlSchemes.Should().HaveCount(2);
    }
}
