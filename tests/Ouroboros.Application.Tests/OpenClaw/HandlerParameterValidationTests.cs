// <copyright file="HandlerParameterValidationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.OpenClaw.PcNode.Handlers;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

/// <summary>
/// Tests parameter validation and error handling for handlers.
/// Does not exercise actual I/O operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class HandlerParameterValidationTests
{
    private static PcNodeSecurityPolicy CreatePolicy(Action<PcNodeSecurityConfig>? configure = null)
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        configure?.Invoke(config);
        return new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
    }

    private static PcNodeSecurityConfig CreateConfig() =>
        PcNodeSecurityConfig.CreateDevelopment();

    private static PcNodeExecutionContext CreateContext() =>
        new("req-1", "device-1", DateTime.UtcNow, new OpenClawAuditLog());

    private static JsonElement EmptyParams() =>
        JsonSerializer.SerializeToElement(new { });

    // ── SystemNotifyHandler ────────────────────────────────────────────

    [Fact]
    public async Task SystemNotifyHandler_MissingMessage_ReturnsFail()
    {
        var handler = new SystemNotifyHandler();
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("message");
    }

    [Fact]
    public async Task SystemNotifyHandler_EmptyMessage_ReturnsFail()
    {
        var handler = new SystemNotifyHandler();
        var parameters = JsonSerializer.SerializeToElement(new { message = "" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // ── ClipboardWriteHandler ──────────────────────────────────────────

    [Fact]
    public async Task ClipboardWriteHandler_MissingText_ReturnsFail()
    {
        var handler = new ClipboardWriteHandler(CreateConfig());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("text");
    }

    [Fact]
    public async Task ClipboardWriteHandler_ExceedsMaxLength_ReturnsFail()
    {
        var config = CreateConfig();
        config.MaxClipboardLength = 10;
        var handler = new ClipboardWriteHandler(config);
        var parameters = JsonSerializer.SerializeToElement(new { text = new string('x', 20) });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("exceeds maximum");
    }

    // ── BrowserOpenHandler ─────────────────────────────────────────────

    [Fact]
    public async Task BrowserOpenHandler_MissingUrl_ReturnsFail()
    {
        var handler = new BrowserOpenHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("url");
    }

    [Fact]
    public async Task BrowserOpenHandler_InvalidUrl_ReturnsFail()
    {
        var handler = new BrowserOpenHandler(CreatePolicy());
        var parameters = JsonSerializer.SerializeToElement(new { url = "not-a-valid-url" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task BrowserOpenHandler_FileScheme_ReturnsFail()
    {
        var handler = new BrowserOpenHandler(CreatePolicy());
        var parameters = JsonSerializer.SerializeToElement(new { url = "file:///etc/passwd" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("scheme");
    }

    // ── FileReadHandler ────────────────────────────────────────────────

    [Fact]
    public async Task FileReadHandler_MissingPath_ReturnsFail()
    {
        var handler = new FileReadHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    [Fact]
    public async Task FileReadHandler_PathOutsideJail_ReturnsFail()
    {
        var policy = CreatePolicy(c => c.AllowedFileDirectories = [@"C:\SafeDir"]);
        var handler = new FileReadHandler(policy);
        var parameters = JsonSerializer.SerializeToElement(new { path = @"C:\Windows\system32\config" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // ── FileWriteHandler ───────────────────────────────────────────────

    [Fact]
    public async Task FileWriteHandler_MissingPath_ReturnsFail()
    {
        var handler = new FileWriteHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    // ── FileListHandler ────────────────────────────────────────────────

    [Fact]
    public async Task FileListHandler_MissingPath_ReturnsFail()
    {
        var handler = new FileListHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    // ── FileDeleteHandler ──────────────────────────────────────────────

    [Fact]
    public async Task FileDeleteHandler_MissingPath_ReturnsFail()
    {
        var handler = new FileDeleteHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("path");
    }

    // ── ProcessKillHandler ─────────────────────────────────────────────

    [Fact]
    public async Task ProcessKillHandler_MissingTarget_ReturnsFail()
    {
        var handler = new ProcessKillHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("target");
    }

    [Fact]
    public async Task ProcessKillHandler_ProtectedProcess_ReturnsFail()
    {
        var handler = new ProcessKillHandler(CreatePolicy());
        var parameters = JsonSerializer.SerializeToElement(new { target = "csrss" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("protected");
    }

    // ── AppLaunchHandler ───────────────────────────────────────────────

    [Fact]
    public async Task AppLaunchHandler_MissingProgram_ReturnsFail()
    {
        var handler = new AppLaunchHandler(CreatePolicy());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("program");
    }

    [Fact]
    public async Task AppLaunchHandler_NonWhitelistedProgram_ReturnsFail()
    {
        var policy = CreatePolicy(c => c.AllowedApplications = new(StringComparer.OrdinalIgnoreCase) { "notepad" });
        var handler = new AppLaunchHandler(policy);
        var parameters = JsonSerializer.SerializeToElement(new { program = "malware" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("whitelist");
    }

    // ── ShellCommandHandler ────────────────────────────────────────────

    [Fact]
    public async Task ShellCommandHandler_MissingCommand_ReturnsFail()
    {
        var handler = new ShellCommandHandler(CreatePolicy(), CreateConfig());
        var result = await handler.ExecuteAsync(EmptyParams(), CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("command");
    }

    [Fact]
    public async Task ShellCommandHandler_DisabledByDefault_ReturnsFail()
    {
        var config = CreateConfig();
        config.EnableShellCommands = false;
        var policy = new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
        var handler = new ShellCommandHandler(policy, config);
        var parameters = JsonSerializer.SerializeToElement(new { command = "echo hello" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task ShellCommandHandler_BlockedPattern_ReturnsFail()
    {
        var config = CreateConfig();
        config.EnableShellCommands = true;
        config.AllowedShellCommands = new(StringComparer.OrdinalIgnoreCase) { "rm" };
        var policy = new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
        var handler = new ShellCommandHandler(policy, config);
        var parameters = JsonSerializer.SerializeToElement(new { command = "rm -rf /" });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // ── ScreenRecordHandler ────────────────────────────────────────────

    [Fact]
    public async Task ScreenRecordHandler_ExceedsDuration_ReturnsFail()
    {
        var config = CreateConfig();
        config.MaxScreenRecordSeconds = 10;
        var handler = new ScreenRecordHandler(config);
        var parameters = JsonSerializer.SerializeToElement(new { duration_seconds = 30 });
        var result = await handler.ExecuteAsync(parameters, CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("exceeds maximum");
    }
}
