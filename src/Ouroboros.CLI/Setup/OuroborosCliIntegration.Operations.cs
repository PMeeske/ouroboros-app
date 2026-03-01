// <copyright file="OuroborosCliIntegration.Operations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.CLI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Integration;
using Ouroboros.Providers;
using Ouroboros.Providers.Tapo;
using Spectre.Console;

/// <summary>
/// Partial class for goal execution, reasoning, telemetry, health, and Tapo configuration validation.
/// </summary>
public static partial class OuroborosCliIntegration
{
    /// <summary>
    /// Executes a goal using the Ouroboros system if initialized.
    /// Falls back gracefully if system is not initialized.
    /// </summary>
    public static async Task<bool> TryExecuteGoalAsync(
        string goal,
        ExecutionConfig? config = null,
        Action<PlanExecutionResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false; // Not initialized, fall back to regular command handling
        }

        config ??= ExecutionConfig.Default;
        _telemetry?.RecordGoalExecution(true, TimeSpan.Zero);

        var result = await _ouroborosCore.ExecuteGoalAsync(goal, config);

        result.Match(
            success =>
            {
                _telemetry?.RecordGoalExecution(true, success.Duration);
                onSuccess?.Invoke(success);
            },
            error =>
            {
                _telemetry?.RecordError("goal_execution", "execution_failed");
                onError?.Invoke(error);
            });

        return true;
    }

    /// <summary>
    /// Performs reasoning using the Ouroboros system if initialized.
    /// </summary>
    public static async Task<bool> TryReasonAsync(
        string query,
        ReasoningConfig? config = null,
        Action<ReasoningResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false;
        }

        config ??= ReasoningConfig.Default;
        var startTime = DateTime.UtcNow;

        var result = await _ouroborosCore.ReasonAboutAsync(query, config);

        var duration = DateTime.UtcNow - startTime;
        _telemetry?.RecordReasoningQuery(
            duration,
            config.UseSymbolicReasoning,
            config.UseCausalInference,
            config.UseAbduction);

        result.Match(
            onSuccess ?? (_ => { }),
            onError ?? (_ => { }));

        return true;
    }

    /// <summary>
    /// Records telemetry for CLI operations.
    /// </summary>
    public static void RecordCliOperation(string operation, bool success, TimeSpan duration)
    {
        _telemetry?.RecordGoalExecution(success, duration, new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["cli"] = true
        });
    }

    /// <summary>
    /// Gets health status of the Ouroboros system.
    /// </summary>
    public static string GetHealthStatus()
    {
        if (_ouroborosCore == null)
        {
            return "Not initialized";
        }

        var status = new System.Text.StringBuilder();
        status.AppendLine("Ouroboros System Status:");
        status.AppendLine($"  Episodic Memory: {(_ouroborosCore.EpisodicMemory != null ? "✓" : "✗")}");
        status.AppendLine($"  MeTTa Reasoning: {(_ouroborosCore.MeTTaReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Hierarchical Planner: {(_ouroborosCore.HierarchicalPlanner != null ? "✓" : "✗")}");
        status.AppendLine($"  Causal Reasoning: {(_ouroborosCore.CausalReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Consciousness: {(_ouroborosCore.Consciousness != null ? "✓" : "✗")}");
        status.AppendLine($"  Reflection: {(_ouroborosCore.Reflection != null ? "✓" : "✗")}");

        return status.ToString();
    }

    /// <summary>
    /// Result of Tapo configuration validation.
    /// </summary>
    private sealed record TapoConfigValidationResult(
        bool IsValid,
        bool UseRtsp,
        bool UseRestApi,
        string? ValidationMessage);

    /// <summary>
    /// Validates Tapo configuration and determines which mode to use.
    /// </summary>
    private static TapoConfigValidationResult ValidateTapoConfiguration(
        string? serverAddress,
        string? username,
        string? password,
        List<TapoDevice>? devices)
    {
        // Check for RTSP mode (direct camera access)
        var hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        var hasDevices = devices?.Count > 0;
        var hasCameraDevices = devices?.Any(d => IsCameraDevice(d.DeviceType)) ?? false;

        if (hasCredentials && hasDevices && hasCameraDevices)
        {
            // Validate device configurations
            foreach (var device in devices!)
            {
                if (string.IsNullOrWhiteSpace(device.Name))
                {
                    return new TapoConfigValidationResult(false, false, false,
                        "Device name is required for all Tapo devices");
                }

                if (string.IsNullOrWhiteSpace(device.IpAddress) || device.IpAddress == "192.168.1.1")
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Warning: Device '{Markup.Escape(device.Name)}' has placeholder IP address. Update appsettings.json with actual camera IP."));
                }
            }

            // Enable REST API alongside RTSP if server address is configured and valid
            var useRestApi = !string.IsNullOrEmpty(serverAddress)
                && Uri.TryCreate(serverAddress, UriKind.Absolute, out var rtspUri)
                && (rtspUri.Scheme == "http" || rtspUri.Scheme == "https");

            return new TapoConfigValidationResult(true, true, useRestApi, null);
        }

        // Check for REST API mode (no cameras, just smart home devices)
        if (!string.IsNullOrEmpty(serverAddress))
        {
            // Validate URL format
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new TapoConfigValidationResult(false, false, false,
                    $"Invalid Tapo server address: {serverAddress}");
            }

            return new TapoConfigValidationResult(true, false, true, null);
        }

        // No Tapo configuration
        return new TapoConfigValidationResult(false, false, false, null);
    }

    /// <summary>
    /// Finds the tapo_gateway.py script by searching known locations.
    /// </summary>
    private static string? FindGatewayScriptPath()
    {
        // Search relative to the application base directory and common repo layouts
        var candidates = new[]
        {
            // When running from ouroboros-app with libs/engine submodule
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "libs", "engine", "tools", "tapo_gateway.py"),
            // When running from meta-repo (Ouroboros-v2)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "engine", "tools", "tapo_gateway.py"),
            // When running from ouroboros-engine directly
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tools", "tapo_gateway.py"),
            // Standalone peer checkout
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ouroboros-engine", "tools", "tapo_gateway.py"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    /// <summary>
    /// Checks if a device type is a camera.
    /// </summary>
    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210
            or TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320
            or TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;
}
