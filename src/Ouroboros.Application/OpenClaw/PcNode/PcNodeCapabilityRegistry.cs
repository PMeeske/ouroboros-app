// <copyright file="PcNodeCapabilityRegistry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Immutable registry of PC node capability handlers.
/// Follows the same pattern as <see cref="Ouroboros.Tools.Tools.ToolRegistry"/>:
/// all mutations return new instances.
/// </summary>
public sealed class PcNodeCapabilityRegistry
{
    private readonly ImmutableDictionary<string, IPcNodeCapabilityHandler> _handlers;

    public PcNodeCapabilityRegistry()
        : this(ImmutableDictionary<string, IPcNodeCapabilityHandler>.Empty
            .WithComparers(StringComparer.OrdinalIgnoreCase))
    {
    }

    private PcNodeCapabilityRegistry(ImmutableDictionary<string, IPcNodeCapabilityHandler> handlers)
    {
        _handlers = handlers;
    }

    /// <summary>
    /// Registers a handler, returning a new registry instance.
    /// </summary>
    public PcNodeCapabilityRegistry WithHandler(IPcNodeCapabilityHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new PcNodeCapabilityRegistry(_handlers.SetItem(handler.CapabilityName, handler));
    }

    /// <summary>
    /// Gets a handler by capability name, or null if not registered.
    /// </summary>
    public IPcNodeCapabilityHandler? GetHandler(string capabilityName) =>
        _handlers.TryGetValue(capabilityName, out var handler) ? handler : null;

    /// <summary>
    /// Returns descriptors for all registered capabilities (for gateway advertisement).
    /// </summary>
    public IEnumerable<CapabilityDescriptor> GetCapabilities() =>
        _handlers.Values.Select(h => new CapabilityDescriptor(
            h.CapabilityName, h.Description, h.ParameterSchema));

    /// <summary>
    /// Returns descriptors for only the enabled capabilities (per security config).
    /// </summary>
    public IEnumerable<CapabilityDescriptor> GetEnabledCapabilities(PcNodeSecurityConfig config) =>
        _handlers.Values
            .Where(h => config.EnabledCapabilities.Contains(h.CapabilityName))
            .Select(h => new CapabilityDescriptor(h.CapabilityName, h.Description, h.ParameterSchema));

    /// <summary>
    /// All registered handler names.
    /// </summary>
    public IEnumerable<string> Names => _handlers.Keys;

    /// <summary>
    /// Number of registered handlers.
    /// </summary>
    public int Count => _handlers.Count;

    /// <summary>
    /// Creates a registry pre-populated with all built-in handlers.
    /// Only capabilities listed in the config will be advertised to the gateway.
    /// </summary>
    public static PcNodeCapabilityRegistry CreateDefault(
        PcNodeSecurityPolicy policy, PcNodeSecurityConfig config)
    {
        var registry = new PcNodeCapabilityRegistry();

        // Low risk
        registry = registry
            .WithHandler(new Handlers.SystemInfoHandler())
            .WithHandler(new Handlers.SystemNotifyHandler())
            .WithHandler(new Handlers.ClipboardReadHandler())
            .WithHandler(new Handlers.ClipboardWriteHandler(config));

        // Medium risk
        registry = registry
            .WithHandler(new Handlers.ScreenCaptureHandler(config))
            .WithHandler(new Handlers.BrowserOpenHandler(policy))
            .WithHandler(new Handlers.FileListHandler(policy))
            .WithHandler(new Handlers.FileReadHandler(policy))
            .WithHandler(new Handlers.FileWriteHandler(policy))
            .WithHandler(new Handlers.ProcessListHandler())
            .WithHandler(new Handlers.AppLaunchHandler(policy))
            .WithHandler(new Handlers.ProcessKillHandler(policy));

        // High risk
        registry = registry
            .WithHandler(new Handlers.ShellCommandHandler(policy, config))
            .WithHandler(new Handlers.FileDeleteHandler(policy))
            .WithHandler(new Handlers.ScreenRecordHandler(config));

        return registry;
    }
}
