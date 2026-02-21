// <copyright file="ServiceDiscoveryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;

/// <summary>
/// Tool that uses Scrutor-assembled <see cref="IServiceProvider"/> to discover
/// and invoke any registered service at runtime.
///
/// Subsystems register themselves via <see cref="ServiceContainerFactory.RegisterSingleton{T}"/>
/// during startup wiring, and <see cref="ServiceContainerFactory.Build"/> scans all Ouroboros
/// assemblies for <see cref="ITool"/> implementations via Scrutor.
///
/// Commands:
///   list                            — list all services registered in the IServiceProvider
///   tools                           — list all ITool implementations discovered by Scrutor
///   invoke ServiceType.MethodName   — invoke a method on a resolved service instance
///   invoke ServiceType.MethodName {"arg":"val"} — invoke with JSON args
/// </summary>
public sealed class ServiceDiscoveryTool : ITool
{
    public string Name => "service_discovery";

    public string Description =>
        "Scrutor-based service discovery and runtime invocation. " +
        "Commands: 'list' (all IServiceProvider registrations), " +
        "'tools' (Scrutor-discovered ITool types), " +
        "'invoke TypeName.MethodName [json_args]' (call any registered service method).";

    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result<string, string>.Ok(BuildHelp());

        var trimmed = input.Trim();

        if (trimmed.Equals("list", StringComparison.OrdinalIgnoreCase))
            return Result<string, string>.Ok(BuildServiceList());

        if (trimmed.Equals("tools", StringComparison.OrdinalIgnoreCase))
            return Result<string, string>.Ok(BuildToolList());

        if (trimmed.StartsWith("invoke ", StringComparison.OrdinalIgnoreCase))
            return await InvokeServiceMethodAsync(trimmed["invoke ".Length..].Trim(), ct);

        return Result<string, string>.Err(
            $"Unknown command '{trimmed}'. Use: list | tools | invoke TypeName.MethodName [args]");
    }

    // ── list ──────────────────────────────────────────────────────────────────

    private static string BuildServiceList()
    {
        var descriptors = ServiceContainerFactory.GetRegisteredServices().ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"IServiceProvider registrations ({descriptors.Count}):");
        sb.AppendLine();

        foreach (var group in descriptors
            .GroupBy(d => d.Lifetime)
            .OrderBy(g => g.Key.ToString()))
        {
            sb.AppendLine($"  [{group.Key}]");
            foreach (var d in group.Take(50))
            {
                var impl = d.ImplementationType?.Name
                    ?? d.ImplementationInstance?.GetType().Name
                    ?? "factory";
                sb.AppendLine($"    {d.ServiceType.Name} → {impl}");
            }
            if (group.Count() > 50)
                sb.AppendLine($"    ... and {group.Count() - 50} more");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── tools ─────────────────────────────────────────────────────────────────

    private static string BuildToolList()
    {
        var provider = ServiceContainerFactory.Provider;
        var tools = provider.GetServices<ITool>().ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Scrutor-discovered ITool implementations ({tools.Count}):");
        sb.AppendLine();

        foreach (var tool in tools.OrderBy(t => t.Name))
            sb.AppendLine($"  • {tool.Name,-30} {tool.Description[..Math.Min(80, tool.Description.Length)]}");

        return sb.ToString();
    }

    // ── invoke ────────────────────────────────────────────────────────────────

    private static async Task<Result<string, string>> InvokeServiceMethodAsync(string spec, CancellationToken ct)
    {
        // Format: TypeName.MethodName [json_args]
        var dotIdx = spec.IndexOf('.');
        if (dotIdx < 0)
            return Result<string, string>.Err("Expected: invoke TypeName.MethodName [json_args]");

        var typeName = spec[..dotIdx].Trim();
        var rest = spec[(dotIdx + 1)..].Trim();
        string methodName;
        string? argsJson = null;

        var spaceIdx = rest.IndexOf(' ');
        if (spaceIdx < 0)
            methodName = rest;
        else
        {
            methodName = rest[..spaceIdx].Trim();
            argsJson = rest[(spaceIdx + 1)..].Trim();
        }

        // Resolve the service from the IServiceProvider
        var provider = ServiceContainerFactory.Provider;
        var serviceType = ResolveServiceType(typeName, provider);
        if (serviceType == null)
            return Result<string, string>.Err($"No service registered for type '{typeName}'. Use 'list' to see available services.");

        var service = provider.GetService(serviceType);
        if (service == null)
            return Result<string, string>.Err($"Service '{typeName}' is registered but could not be resolved.");

        var method = service.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
            .FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

        if (method == null)
            return Result<string, string>.Err($"Method '{methodName}' not found on '{typeName}'.");

        try
        {
            var args = BuildArguments(method, argsJson, ct);
            var rawResult = method.Invoke(service, args);

            string output = rawResult switch
            {
                Task<string> ts  => await ts,
                Task<object> to  => (await to)?.ToString() ?? "(null)",
                Task t           => await t.ContinueWith(_ => "(void)", ct),
                string s         => s,
                null             => "(null)",
                _                => rawResult.ToString() ?? "(object)"
            };

            return Result<string, string>.Ok(output);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Err($"Invocation failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Type? ResolveServiceType(string typeName, IServiceProvider provider)
    {
        // Try exact match first, then suffix match on ServiceType name
        foreach (var descriptor in ServiceContainerFactory.GetRegisteredServices())
        {
            var st = descriptor.ServiceType;
            if (st.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
             || st.FullName?.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true)
                return st;
        }

        // Also check implementation types
        foreach (var descriptor in ServiceContainerFactory.GetRegisteredServices())
        {
            var it = descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType();
            if (it?.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) == true)
                return descriptor.ServiceType;
        }

        return null;
    }

    private static object?[] BuildArguments(MethodInfo method, string? argsJson, CancellationToken ct)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return [];

        var result = new object?[parameters.Length];
        JsonElement? jsonRoot = null;

        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try { jsonRoot = JsonDocument.Parse(argsJson).RootElement; }
            catch { /* plain-string fallback */ }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if (p.ParameterType == typeof(CancellationToken))
            {
                result[i] = ct;
                continue;
            }

            if (jsonRoot.HasValue && jsonRoot.Value.ValueKind == JsonValueKind.Object
                && jsonRoot.Value.TryGetProperty(p.Name ?? "", out var propEl))
            {
                result[i] = JsonSerializer.Deserialize(propEl.GetRawText(), p.ParameterType);
                continue;
            }

            if (p.ParameterType == typeof(string) && !string.IsNullOrWhiteSpace(argsJson)
                && jsonRoot?.ValueKind != JsonValueKind.Object)
            {
                result[i] = argsJson;
                continue;
            }

            result[i] = p.HasDefaultValue ? p.DefaultValue : null;
        }

        return result;
    }

    private static string BuildHelp() =>
        """
        service_discovery — Scrutor + IServiceProvider runtime discovery tool.

        Commands:
          list                             All types registered in the IServiceProvider
          tools                            All ITool implementations discovered by Scrutor scan
          invoke TypeName.MethodName       Call a method on a resolved service (no args)
          invoke TypeName.MethodName args  Call with plain string or JSON object argument

        Examples:
          service_discovery list
          service_discovery tools
          service_discovery invoke AutonomousMind.GetStatus
          service_discovery invoke QdrantNeuralMemory.GetStatsAsync
          service_discovery invoke AutonomousMind.AddInterest "consciousness"
        """;
}
