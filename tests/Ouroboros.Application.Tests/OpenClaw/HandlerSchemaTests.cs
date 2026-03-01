// <copyright file="HandlerSchemaTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.OpenClaw;
using Ouroboros.Application.OpenClaw.PcNode;
using Ouroboros.Application.OpenClaw.PcNode.Handlers;
using Xunit;

namespace Ouroboros.Tests.OpenClaw;

/// <summary>
/// Tests that handlers which accept parameters provide valid JSON schemas,
/// and those that do not have null schemas.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "OpenClaw")]
public class HandlerSchemaTests
{
    private static PcNodeSecurityPolicy CreatePolicy()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        return new PcNodeSecurityPolicy(config, new OpenClawAuditLog());
    }

    private static PcNodeSecurityConfig CreateConfig() =>
        PcNodeSecurityConfig.CreateDevelopment();

    [Fact]
    public void SystemInfoHandler_HasNoSchema()
    {
        new SystemInfoHandler().ParameterSchema.Should().BeNull();
    }

    [Fact]
    public void ClipboardReadHandler_HasNoSchema()
    {
        new ClipboardReadHandler().ParameterSchema.Should().BeNull();
    }

    [Fact]
    public void SystemNotifyHandler_HasSchema()
    {
        var schema = new SystemNotifyHandler().ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("message");
    }

    [Fact]
    public void ClipboardWriteHandler_HasSchema()
    {
        var schema = new ClipboardWriteHandler(CreateConfig()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("text");
    }

    [Fact]
    public void BrowserOpenHandler_HasSchema()
    {
        var schema = new BrowserOpenHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("url");
    }

    [Fact]
    public void FileReadHandler_HasSchema()
    {
        var schema = new FileReadHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("path");
    }

    [Fact]
    public void FileWriteHandler_HasSchema()
    {
        var schema = new FileWriteHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("path");
        schema.Should().Contain("content");
    }

    [Fact]
    public void FileDeleteHandler_HasSchema()
    {
        var schema = new FileDeleteHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("path");
    }

    [Fact]
    public void FileListHandler_HasSchema()
    {
        var schema = new FileListHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("path");
    }

    [Fact]
    public void ProcessListHandler_HasSchema()
    {
        var schema = new ProcessListHandler().ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("filter");
    }

    [Fact]
    public void ProcessKillHandler_HasSchema()
    {
        var schema = new ProcessKillHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("target");
    }

    [Fact]
    public void AppLaunchHandler_HasSchema()
    {
        var schema = new AppLaunchHandler(CreatePolicy()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("program");
    }

    [Fact]
    public void ShellCommandHandler_HasSchema()
    {
        var schema = new ShellCommandHandler(CreatePolicy(), CreateConfig()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("command");
    }

    [Fact]
    public void ScreenCaptureHandler_HasSchema()
    {
        var schema = new ScreenCaptureHandler(CreateConfig()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("monitor");
    }

    [Fact]
    public void ScreenRecordHandler_HasSchema()
    {
        var schema = new ScreenRecordHandler(CreateConfig()).ParameterSchema;
        schema.Should().NotBeNull();
        schema.Should().Contain("duration_seconds");
    }
}
