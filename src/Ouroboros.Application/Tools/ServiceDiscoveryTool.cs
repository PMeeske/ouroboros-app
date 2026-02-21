// <copyright file="ServiceDiscoveryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;

/// <summary>
/// Static registry of named service instances that Iaret can call at runtime.
/// Subsystems register themselves here during wiring so the DI scan tool can discover and invoke them.
/// </summary>
public static class ServiceRegistry
{
    private static readonly Dictionary<string, object> _services = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a service under a given name.</summary>
    public static void Register(string name, object instance)
    {
        _services[name] = instance;
    }

    /// <summary>Returns all registered services.</summary>
    public static IReadOnlyDictionary<string, object> All => _services;

    /// <summary>Tries to retrieve a registered service.</summary>
    public static bool TryGet(string name, out object? service)
        => _services.TryGetValue(name, out service);
}

/// <summary>
/// Tool that scans loaded assemblies for service interfaces and callable methods,
/// and allows Iaret to invoke any registered service method at runtime.
/// Emulates Scrutor-style assembly scanning without the DI package.
///
/// Commands:
///   list                     — list all registered services and their public methods
///   scan                     — scan current assemblies for ITool implementations
///   invoke ServiceName.MethodName [json_args]  — call a service method
/// </summary>
public sealed class ServiceDiscoveryTool : ITool
{
    public string Name => "service_discovery";

    public string Description =>
        "Scans available services and calls their methods. " +
        "Use 'list' to see all registered services, 'scan' to discover ITool implementations in loaded assemblies, " +
        "or 'invoke ServiceName.MethodName {\"arg\":\"value\"}' to call a service method.";

    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result<string, string>.Ok(BuildHelp());

        var trimmed = input.Trim();

        if (trimmed.Equals("list", StringComparison.OrdinalIgnoreCase))
            return Result<string, string>.Ok(BuildServiceList());

        if (trimmed.Equals("scan", StringComparison.OrdinalIgnoreCase))
            return Result<string, string>.Ok(await ScanAssembliesAsync());

        if (trimmed.StartsWith("invoke ", StringComparison.OrdinalIgnoreCase))
            return await InvokeServiceMethodAsync(trimmed["invoke ".Length..].Trim(), ct);

        return Result<string, string>.Err($"Unknown command '{trimmed}'. Use: list | scan | invoke ServiceName.MethodName [args]");
    }

    // ── list ──────────────────────────────────────────────────────────────────

    private static string BuildServiceList()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Registered services ({ServiceRegistry.All.Count}):");

        foreach (var (name, svc) in ServiceRegistry.All.OrderBy(kv => kv.Key))
        {
            var methods = GetCallableMethods(svc.GetType());
            sb.AppendLine();
            sb.AppendLine($"  [{name}] ({svc.GetType().Name})");
            foreach (var method in methods.Take(10))
            {
                var paramList = string.Join(", ", method.GetParameters()
                    .Where(p => p.ParameterType != typeof(CancellationToken))
                    .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                sb.AppendLine($"    • {method.Name}({paramList}) → {method.ReturnType.Name}");
            }

            if (methods.Count > 10)
                sb.AppendLine($"    ... and {methods.Count - 10} more");
        }

        return sb.Length > 0 ? sb.ToString() : "No services registered. Subsystems call ServiceRegistry.Register() during wiring.";
    }

    // ── scan ──────────────────────────────────────────────────────────────────

    private static Task<string> ScanAssembliesAsync()
    {
        var sb = new StringBuilder();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ouroboros") == true)
            .ToList();

        sb.AppendLine($"Scanning {assemblies.Count} Ouroboros assemblies for ITool implementations...");
        sb.AppendLine();

        int count = 0;
        foreach (var asm in assemblies)
        {
            try
            {
                var toolTypes = asm.GetExportedTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ITool).IsAssignableFrom(t))
                    .ToList();

                if (toolTypes.Count == 0) continue;

                sb.AppendLine($"  {asm.GetName().Name}:");
                foreach (var t in toolTypes)
                    sb.AppendLine($"    • {t.Name}");

                count += toolTypes.Count;
            }
            catch
            {
                // Skip assemblies that can't be reflected
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total ITool implementations found: {count}");
        return Task.FromResult(sb.ToString());
    }

    // ── invoke ────────────────────────────────────────────────────────────────

    private static async Task<Result<string, string>> InvokeServiceMethodAsync(string spec, CancellationToken ct)
    {
        // Format: ServiceName.MethodName [json_args_object]
        var dotIdx = spec.IndexOf('.');
        if (dotIdx < 0)
            return Result<string, string>.Err("Expected format: invoke ServiceName.MethodName [json_args]");

        var serviceName = spec[..dotIdx].Trim();
        var rest = spec[(dotIdx + 1)..].Trim();
        string methodName;
        string? argsJson = null;

        var spaceIdx = rest.IndexOf(' ');
        if (spaceIdx < 0)
        {
            methodName = rest;
        }
        else
        {
            methodName = rest[..spaceIdx].Trim();
            argsJson = rest[(spaceIdx + 1)..].Trim();
        }

        if (!ServiceRegistry.TryGet(serviceName, out var service) || service == null)
            return Result<string, string>.Err($"Service '{serviceName}' not found. Use 'list' to see registered services.");

        var method = GetCallableMethods(service.GetType())
            .FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

        if (method == null)
            return Result<string, string>.Err($"Method '{methodName}' not found on '{serviceName}'. Use 'list' to see available methods.");

        try
        {
            var args = BuildArguments(method, argsJson, ct);
            var rawResult = method.Invoke(service, args);

            string output = rawResult switch
            {
                Task<string> ts    => await ts,
                Task<object> to    => (await to)?.ToString() ?? "(null)",
                Task t             => await t.ContinueWith(_ => "(void)"),
                string s           => s,
                null               => "(null)",
                _                  => rawResult.ToString() ?? "(object)"
            };

            return Result<string, string>.Ok(output);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Err($"Invocation failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static List<MethodInfo> GetCallableMethods(Type type)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
               .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
               .ToList();

    private static object?[] BuildArguments(MethodInfo method, string? argsJson, CancellationToken ct)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return [];

        var result = new object?[parameters.Length];
        JsonElement? jsonDoc = null;

        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try
            {
                jsonDoc = JsonDocument.Parse(argsJson).RootElement;
            }
            catch { /* plain string fallback */ }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if (p.ParameterType == typeof(CancellationToken))
            {
                result[i] = ct;
                continue;
            }

            if (jsonDoc.HasValue && jsonDoc.Value.ValueKind == JsonValueKind.Object
                && jsonDoc.Value.TryGetProperty(p.Name ?? "", out var propEl))
            {
                result[i] = JsonSerializer.Deserialize(propEl.GetRawText(), p.ParameterType);
                continue;
            }

            // Positional plain-string for single string param
            if (p.ParameterType == typeof(string) && !string.IsNullOrWhiteSpace(argsJson)
                && jsonDoc?.ValueKind != JsonValueKind.Object)
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
        service_discovery tool — Scrutor-style assembly scanning and runtime service invocation.

        Commands:
          list                           List all registered services and their callable methods
          scan                           Scan Ouroboros assemblies for ITool implementations
          invoke ServiceName.Method      Call a method on a registered service (no args)
          invoke ServiceName.Method args Call with a JSON object or plain string argument

        Examples:
          service_discovery list
          service_discovery scan
          service_discovery invoke Memory.GetStatsAsync
          service_discovery invoke AutonomousMind.AddInterest "philosophy"
        """;
}
