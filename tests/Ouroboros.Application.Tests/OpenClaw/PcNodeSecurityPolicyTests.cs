// <copyright file="PcNodeSecurityPolicyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class PcNodeSecurityPolicyTests
{
    private static PcNodeSecurityPolicy CreatePolicy(Action<PcNodeSecurityConfig>? configure = null)
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        configure?.Invoke(config);
        var auditLog = new OpenClawAuditLog();
        return new PcNodeSecurityPolicy(config, auditLog);
    }

    // ── Incoming Invoke Validation ──────────────────────────────────────────

    [Fact]
    public void ValidateIncomingInvoke_DeniesDisabledCapability()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateIncomingInvoke(
            "test-device", "file.write", null, PcNodeRiskLevel.Medium);

        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not enabled");
    }

    [Fact]
    public void ValidateIncomingInvoke_AllowsEnabledCapability()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateIncomingInvoke(
            "test-device", "system.info", null, PcNodeRiskLevel.Low);

        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateIncomingInvoke_DeniesUnauthorizedCaller()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedCallerDeviceIds = ["specific-device"];
        });

        var verdict = policy.ValidateIncomingInvoke(
            "wrong-device", "system.info", null, PcNodeRiskLevel.Low);

        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not authorized");
    }

    [Fact]
    public void ValidateIncomingInvoke_AllowsWildcardCaller()
    {
        var policy = CreatePolicy();
        // Development config uses ["*"]
        var verdict = policy.ValidateIncomingInvoke(
            "any-device-id", "system.info", null, PcNodeRiskLevel.Low);

        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateIncomingInvoke_DeniesWhenNoCallersConfigured()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedCallerDeviceIds = [];
        });

        var verdict = policy.ValidateIncomingInvoke(
            "any-device", "system.info", null, PcNodeRiskLevel.Low);

        verdict.IsAllowed.Should().BeFalse();
    }

    // ── File Path Validation ────────────────────────────────────────────────

    [Fact]
    public void ValidateFilePath_DeniesPathOutsideAllowedDirectories()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedFileDirectories = [@"C:\Allowed"];
        });

        var verdict = policy.ValidateFilePath(@"C:\NotAllowed\secret.txt", FileOperation.Read);
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not in any allowed directory");
    }

    [Fact]
    public void ValidateFilePath_AllowsPathInAllowedDirectory()
    {
        var tempDir = Path.GetTempPath();
        var policy = CreatePolicy(c =>
        {
            c.AllowedFileDirectories = [tempDir];
            c.BlockedFilePaths = []; // Clear defaults
        });

        var verdict = policy.ValidateFilePath(
            Path.Combine(tempDir, "test.txt"), FileOperation.Read);
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilePath_DeniesBlockedExtensionForWrite()
    {
        var tempDir = Path.GetTempPath();
        var policy = CreatePolicy(c =>
        {
            c.AllowedFileDirectories = [tempDir];
            c.BlockedFilePaths = [];
        });

        var verdict = policy.ValidateFilePath(
            Path.Combine(tempDir, "malware.exe"), FileOperation.Write);
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain(".exe");
    }

    [Fact]
    public void ValidateFilePath_AllowsBlockedExtensionForRead()
    {
        var tempDir = Path.GetTempPath();
        var policy = CreatePolicy(c =>
        {
            c.AllowedFileDirectories = [tempDir];
            c.BlockedFilePaths = [];
        });

        var verdict = policy.ValidateFilePath(
            Path.Combine(tempDir, "program.exe"), FileOperation.Read);
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateFilePath_DeniesEmptyAllowedDirectories()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedFileDirectories = [];
        });

        var verdict = policy.ValidateFilePath(@"C:\any\file.txt", FileOperation.Read);
        verdict.IsAllowed.Should().BeFalse();
    }

    // ── URL Validation ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateUrl_AllowsHttps()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateUrl("https://example.com");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateUrl_DeniesFileScheme()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateUrl("file:///etc/passwd");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("scheme");
    }

    [Fact]
    public void ValidateUrl_DeniesBlockedDomain()
    {
        var policy = CreatePolicy(c =>
        {
            c.BlockedUrlDomains = ["malware.example.com"];
        });

        var verdict = policy.ValidateUrl("https://malware.example.com/payload");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("blocked");
    }

    [Fact]
    public void ValidateUrl_DeniesInvalidUrl()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateUrl("not-a-url");
        verdict.IsAllowed.Should().BeFalse();
    }

    // ── Shell Command Validation ────────────────────────────────────────────

    [Fact]
    public void ValidateShellCommand_DeniesWhenDisabled()
    {
        var policy = CreatePolicy(c =>
        {
            c.EnableShellCommands = false;
        });

        var verdict = policy.ValidateShellCommand("git status");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("disabled");
    }

    [Fact]
    public void ValidateShellCommand_DeniesBlockedPattern()
    {
        var policy = CreatePolicy(c =>
        {
            c.EnableShellCommands = true;
            c.AllowedShellCommands = ["rm"];
        });

        var verdict = policy.ValidateShellCommand("rm -rf /");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("blocked pattern");
    }

    [Fact]
    public void ValidateShellCommand_AllowsWhitelistedCommand()
    {
        var policy = CreatePolicy(c =>
        {
            c.EnableShellCommands = true;
            c.AllowedShellCommands = ["git status", "dotnet build"];
        });

        var verdict = policy.ValidateShellCommand("git status");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateShellCommand_DeniesNonWhitelistedCommand()
    {
        var policy = CreatePolicy(c =>
        {
            c.EnableShellCommands = true;
            c.AllowedShellCommands = ["git status"];
        });

        var verdict = policy.ValidateShellCommand("curl http://evil.com");
        verdict.IsAllowed.Should().BeFalse();
    }

    // ── Process Validation ──────────────────────────────────────────────────

    [Fact]
    public void ValidateProcess_DeniesProtectedProcessKill()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateProcess("csrss", ProcessOperation.Kill);
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("protected");
    }

    [Fact]
    public void ValidateProcess_DeniesNonWhitelistedLaunch()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedApplications = ["notepad"];
        });

        var verdict = policy.ValidateProcess("powershell", ProcessOperation.Launch);
        verdict.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateProcess_AllowsWhitelistedLaunch()
    {
        var policy = CreatePolicy(c =>
        {
            c.AllowedApplications = ["notepad", "code"];
        });

        var verdict = policy.ValidateProcess("notepad", ProcessOperation.Launch);
        verdict.IsAllowed.Should().BeTrue();
    }

    // ── Sensitive Data Scanning ─────────────────────────────────────────────

    [Fact]
    public void ValidateOutboundContent_BlocksApiKey()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateOutboundContent(
            "config: api_key=sk-ABCDEFGHIJKLMNOPQRSTuvwxyz1234567890");
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("sensitive data");
    }

    [Fact]
    public void ValidateOutboundContent_BlocksAwsKey()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateOutboundContent("AKIAIOSFODNN7EXAMPLE");
        verdict.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateOutboundContent_AllowsNormalContent()
    {
        var policy = CreatePolicy();
        var verdict = policy.ValidateOutboundContent("Hello, this is normal text.");
        verdict.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateOutboundContent_SkipsWhenDisabled()
    {
        var policy = CreatePolicy(c =>
        {
            c.ScanOutboundResults = false;
        });

        var verdict = policy.ValidateOutboundContent(
            "api_key=sk-ABCDEFGHIJKLMNOPQRSTuvwxyz1234567890");
        verdict.IsAllowed.Should().BeTrue();
    }

    // ── Rate Limiting ───────────────────────────────────────────────────────

    [Fact]
    public void ValidateIncomingInvoke_EnforcesGlobalRateLimit()
    {
        var policy = CreatePolicy(c =>
        {
            c.GlobalRateLimitPerMinute = 3;
        });

        // First 3 should pass
        for (int i = 0; i < 3; i++)
        {
            var v = policy.ValidateIncomingInvoke(
                "device", "system.info", null, PcNodeRiskLevel.Low);
            v.IsAllowed.Should().BeTrue($"invocation {i + 1} should be allowed");
        }

        // 4th should be rate limited
        var verdict = policy.ValidateIncomingInvoke(
            "device", "system.info", null, PcNodeRiskLevel.Low);
        verdict.IsAllowed.Should().BeFalse();
        verdict.Reason.Should().Contain("Rate limit");
    }

    // ── Approval ────────────────────────────────────────────────────────────

    [Fact]
    public void RequiresApproval_TrueForHighRiskAboveThreshold()
    {
        var policy = CreatePolicy(c =>
        {
            c.ApprovalThreshold = PcNodeRiskLevel.High;
        });

        policy.RequiresApproval(PcNodeRiskLevel.High, false).Should().BeTrue();
        policy.RequiresApproval(PcNodeRiskLevel.Critical, false).Should().BeTrue();
    }

    [Fact]
    public void RequiresApproval_FalseForLowRiskBelowThreshold()
    {
        var policy = CreatePolicy(c =>
        {
            c.ApprovalThreshold = PcNodeRiskLevel.High;
        });

        policy.RequiresApproval(PcNodeRiskLevel.Low, false).Should().BeFalse();
        policy.RequiresApproval(PcNodeRiskLevel.Medium, false).Should().BeFalse();
    }

    [Fact]
    public void RequiresApproval_TrueWhenHandlerRequiresIt()
    {
        var policy = CreatePolicy(c =>
        {
            c.ApprovalThreshold = PcNodeRiskLevel.Critical;
        });

        // Even low risk requires approval if handler says so
        policy.RequiresApproval(PcNodeRiskLevel.Low, true).Should().BeTrue();
    }
}
