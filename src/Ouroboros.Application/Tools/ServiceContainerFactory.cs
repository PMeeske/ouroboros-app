// <copyright file="ServiceContainerFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Tools;

/// <summary>
/// Builds and owns the application <see cref="IServiceProvider"/> used by
/// <see cref="ServiceDiscoveryTool"/> for Scrutor-based service discovery and invocation.
///
/// Call <see cref="Build"/> once at startup (after all subsystems are wired),
/// then register subsystem instances with <see cref="RegisterSingleton{T}"/> as they become available.
/// The provider is rebuilt lazily on first use if new registrations arrive after initial build.
/// </summary>
public static class ServiceContainerFactory
{
    private static readonly IServiceCollection _services = new ServiceCollection();
    private static IServiceProvider? _provider;
    private static bool _scanned;

    /// <summary>
    /// The current <see cref="IServiceProvider"/>.
    /// Rebuilt automatically when registrations change after first build.
    /// </summary>
    public static IServiceProvider Provider => _provider ??= Build();

    /// <summary>
    /// Registers a singleton instance under its concrete type and all its interfaces.
    /// Call this from subsystem wiring before the provider is first used.
    /// </summary>
    public static void RegisterSingleton<T>(T instance) where T : class
    {
        _services.AddSingleton(instance);
        foreach (var iface in typeof(T).GetInterfaces())
            _services.AddSingleton(iface, instance);

        // Invalidate cached provider so next access rebuilds with new registrations
        _provider = null;
    }

    /// <summary>
    /// Registers a singleton instance under a given service type.
    /// </summary>
    public static void RegisterSingleton(Type serviceType, object instance)
    {
        _services.AddSingleton(serviceType, instance);
        _provider = null;
    }

    /// <summary>
    /// Triggers a Scrutor assembly scan for <see cref="ITool"/> implementations
    /// across all loaded Ouroboros assemblies, then builds (or rebuilds) the provider.
    /// </summary>
    public static IServiceProvider Build()
    {
        if (!_scanned)
        {
            ScanAssemblies();
            _scanned = true;
        }

        _provider = _services.BuildServiceProvider();
        return _provider;
    }

    /// <summary>
    /// Enumerates all services registered in the container.
    /// </summary>
    public static IEnumerable<ServiceDescriptor> GetRegisteredServices() => _services;

    // ── Scrutor assembly scan ──────────────────────────────────────────────

    private static void ScanAssemblies()
    {
        // Use Scrutor to scan all loaded Ouroboros assemblies and register every
        // concrete ITool implementation as a transient service (both as ITool and as itself).
        var ouroborosAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ouroboros") == true)
            .ToArray();

        _services.Scan(scan => scan
            .FromAssemblies(ouroborosAssemblies)
            .AddClasses(c => c.AssignableTo<ITool>(), publicOnly: true)
            .AsSelfWithInterfaces()
            .WithTransientLifetime());
    }
}
