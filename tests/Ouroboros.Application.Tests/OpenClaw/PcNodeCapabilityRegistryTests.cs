// <copyright file="PcNodeCapabilityRegistryTests.cs" company="Ouroboros">
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
public class PcNodeCapabilityRegistryTests
{
    [Fact]
    public void NewRegistry_IsEmpty()
    {
        var registry = new PcNodeCapabilityRegistry();
        registry.Count.Should().Be(0);
        registry.Names.Should().BeEmpty();
    }

    [Fact]
    public void WithHandler_AddsHandler()
    {
        var registry = new PcNodeCapabilityRegistry()
            .WithHandler(new TestHandler("test.capability"));

        registry.Count.Should().Be(1);
        registry.Names.Should().Contain("test.capability");
    }

    [Fact]
    public void WithHandler_ReturnsNewInstance()
    {
        var original = new PcNodeCapabilityRegistry();
        var updated = original.WithHandler(new TestHandler("test.cap"));

        original.Count.Should().Be(0);
        updated.Count.Should().Be(1);
    }

    [Fact]
    public void GetHandler_ReturnsRegisteredHandler()
    {
        var handler = new TestHandler("test.cap");
        var registry = new PcNodeCapabilityRegistry().WithHandler(handler);

        registry.GetHandler("test.cap").Should().BeSameAs(handler);
    }

    [Fact]
    public void GetHandler_IsCaseInsensitive()
    {
        var handler = new TestHandler("System.Info");
        var registry = new PcNodeCapabilityRegistry().WithHandler(handler);

        registry.GetHandler("system.info").Should().BeSameAs(handler);
        registry.GetHandler("SYSTEM.INFO").Should().BeSameAs(handler);
    }

    [Fact]
    public void GetHandler_ReturnsNullForUnregistered()
    {
        var registry = new PcNodeCapabilityRegistry();
        registry.GetHandler("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetCapabilities_ReturnsDescriptors()
    {
        var registry = new PcNodeCapabilityRegistry()
            .WithHandler(new TestHandler("cap.one", "First capability"))
            .WithHandler(new TestHandler("cap.two", "Second capability"));

        var capabilities = registry.GetCapabilities().ToList();
        capabilities.Should().HaveCount(2);
        capabilities.Should().Contain(c => c.Name == "cap.one");
        capabilities.Should().Contain(c => c.Name == "cap.two");
    }

    [Fact]
    public void GetEnabledCapabilities_FiltersbyConfig()
    {
        var registry = new PcNodeCapabilityRegistry()
            .WithHandler(new TestHandler("cap.enabled"))
            .WithHandler(new TestHandler("cap.disabled"));

        var config = new PcNodeSecurityConfig
        {
            EnabledCapabilities = new(StringComparer.OrdinalIgnoreCase) { "cap.enabled" },
        };

        var enabled = registry.GetEnabledCapabilities(config).ToList();
        enabled.Should().HaveCount(1);
        enabled[0].Name.Should().Be("cap.enabled");
    }

    [Fact]
    public void CreateDefault_RegistersAllBuiltInHandlers()
    {
        var config = PcNodeSecurityConfig.CreateDevelopment();
        var auditLog = new OpenClawAuditLog();
        var policy = new PcNodeSecurityPolicy(config, auditLog);
        var registry = PcNodeCapabilityRegistry.CreateDefault(policy, config);

        // Should have all 15 handlers
        registry.Count.Should().Be(15);

        // Verify key handlers exist
        registry.GetHandler("system.info").Should().NotBeNull();
        registry.GetHandler("system.notify").Should().NotBeNull();
        registry.GetHandler("clipboard.read").Should().NotBeNull();
        registry.GetHandler("clipboard.write").Should().NotBeNull();
        registry.GetHandler("screen.capture").Should().NotBeNull();
        registry.GetHandler("browser.open").Should().NotBeNull();
        registry.GetHandler("file.list").Should().NotBeNull();
        registry.GetHandler("file.read").Should().NotBeNull();
        registry.GetHandler("file.write").Should().NotBeNull();
        registry.GetHandler("process.list").Should().NotBeNull();
        registry.GetHandler("app.launch").Should().NotBeNull();
        registry.GetHandler("process.kill").Should().NotBeNull();
        registry.GetHandler("system.run").Should().NotBeNull();
        registry.GetHandler("file.delete").Should().NotBeNull();
        registry.GetHandler("screen.record").Should().NotBeNull();
    }

    [Fact]
    public void WithHandler_OverwritesExisting()
    {
        var handler1 = new TestHandler("test.cap", "First");
        var handler2 = new TestHandler("test.cap", "Second");

        var registry = new PcNodeCapabilityRegistry()
            .WithHandler(handler1)
            .WithHandler(handler2);

        registry.Count.Should().Be(1);
        registry.GetHandler("test.cap")!.Description.Should().Be("Second");
    }

    // ── Test Helper ─────────────────────────────────────────────────────────

    private sealed class TestHandler : IPcNodeCapabilityHandler
    {
        public TestHandler(string name, string description = "Test handler")
        {
            CapabilityName = name;
            Description = description;
        }

        public string CapabilityName { get; }
        public string Description { get; }
        public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Low;
        public string? ParameterSchema => null;
        public bool RequiresApproval => false;

        public Task<PcNodeResult> ExecuteAsync(
            JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
            => Task.FromResult(PcNodeResult.Ok("test"));
    }
}
