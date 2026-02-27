// <copyright file="OuroborosCliIntegration.Tapo.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.CLI;

using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Providers.Tapo;
using Spectre.Console;

/// <summary>
/// Partial class for Tapo embodiment initialization and connectivity.
/// </summary>
public static partial class OuroborosCliIntegration
{
    /// <summary>
    /// Initializes Tapo embodiment - either via REST API authentication or RTSP connection.
    /// REST API availability is checked before attempting connection.
    /// </summary>
    private static async Task TryInitializeTapoAsync(IConfiguration config)
    {
        // Try starting the gateway if registered
        var gateway = _serviceProvider?.GetService<TapoGatewayManager>();
        if (gateway != null)
        {
            await TryStartGatewayAsync(gateway, config);
        }

        var tapoProvider = _serviceProvider?.GetService<TapoEmbodimentProvider>();
        if (tapoProvider == null)
        {
            return; // Tapo not configured
        }

        // Initialize both RTSP cameras AND REST API devices (both can be active simultaneously)
        var hasRtsp = tapoProvider.RtspClientFactory != null;
        var hasRestApi = tapoProvider.RestClient != null;

        if (!hasRtsp && !hasRestApi)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] No RTSP cameras or REST API configured"));
            return;
        }

        // First initialize RTSP cameras (direct camera access)
        if (hasRtsp)
        {
            await InitializeRtspCamerasAsync(tapoProvider);
        }

        // Then initialize REST API (smart plugs/lights control)
        if (hasRestApi)
        {
            await InitializeRestApiAsync(tapoProvider, config);
        }

        // Now connect the provider (initializes both RTSP and REST if configured)
        if (!tapoProvider.IsConnected)
        {
            var connectResult = await tapoProvider.ConnectAsync();
            if (connectResult.IsFailure)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Connection failed: {Markup.Escape(connectResult.Error.ToString())}"));
            }
            else
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Embodiment provider connected successfully"));
            }
        }
    }

    /// <summary>
    /// Starts the Tapo Gateway process with auto-discovery.
    /// </summary>
    private static async Task TryStartGatewayAsync(TapoGatewayManager gateway, IConfiguration config)
    {
        var username = config["Tapo:Username"];
        var password = config["Tapo:Password"];
        var serverPassword = config["Tapo:ServerPassword"] ?? "ouroboros-gateway";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return;

        AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Starting Tapo Gateway with auto-discovery..."));

        var started = await gateway.StartAsync(username, password, serverPassword, port: 8123);
        if (!started)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn("[Tapo] Gateway failed to start (Python 3 + tapo + fastapi + uvicorn required)"));
            return;
        }

        _tapoGateway = gateway;
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] Gateway running on port {gateway.Port}"));

        // Create REST client pointing to the gateway and inject it into the provider
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(gateway.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var restClient = new TapoRestClient(httpClient);

        // Authenticate with the gateway
        var authResult = await restClient.LoginAsync(serverPassword);
        if (authResult.IsFailure)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] Gateway auth failed: {Markup.Escape(authResult.Error.ToString())}"));
            return;
        }

        // Log discovered devices
        var devicesResult = await restClient.GetDevicesAsync();
        if (devicesResult.IsSuccess)
        {
            var devices = devicesResult.Value;
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] Gateway discovered {devices.Count} device(s):"));
            foreach (var device in devices)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo]   - {Markup.Escape(device.Name)} ({device.DeviceType}) @ {Markup.Escape(device.IpAddress)}"));
            }
        }

        // Wire the REST client into the embodiment provider if registered
        var tapoProvider = _serviceProvider?.GetService<TapoEmbodimentProvider>();
        if (tapoProvider != null)
        {
            tapoProvider.SetRestClient(restClient);
        }
    }

    /// <summary>
    /// Initializes RTSP cameras and displays available cameras.
    /// </summary>
    private static Task InitializeRtspCamerasAsync(TapoEmbodimentProvider tapoProvider)
    {
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] RTSP camera support enabled"));
        var cameraNames = tapoProvider.RtspClientFactory!.GetCameraNames().ToList();
        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] {cameraNames.Count} camera(s) registered:"));

        // Display each camera (non-blocking, just log)
        foreach (var cameraName in cameraNames)
        {
            var rtspClient = tapoProvider.RtspClientFactory.GetClient(cameraName);
            if (rtspClient != null)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo]   - {Markup.Escape(cameraName)} @ {Markup.Escape(rtspClient.CameraIp)}"));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes REST API connection with availability check.
    /// </summary>
    private static async Task InitializeRestApiAsync(TapoEmbodimentProvider tapoProvider, IConfiguration config)
    {
        var serverPassword = config["Tapo:ServerPassword"];
        var serverAddress = config["Tapo:ServerAddress"];

        if (string.IsNullOrEmpty(serverPassword))
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] REST API configured but no server password set (Tapo:ServerPassword)"));
            return;
        }

        // Check if REST API server is reachable before attempting authentication
        var isAvailable = await CheckRestApiAvailabilityAsync(serverAddress);
        if (!isAvailable)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim($"[Tapo] REST API server not available at {Markup.Escape(serverAddress ?? "")} (this is optional)"));
            return;
        }

        var authResult = await tapoProvider.AuthenticateAsync(serverPassword);
        if (authResult.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Dim("[Tapo] Connected to Tapo REST API"));
        }
        else
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Warn($"[Tapo] REST API authentication failed: {Markup.Escape(authResult.Error.ToString())}"));
        }
    }

    /// <summary>
    /// Checks if the Tapo REST API server is reachable.
    /// </summary>
    private static async Task<bool> CheckRestApiAvailabilityAsync(string? serverAddress)
    {
        if (string.IsNullOrEmpty(serverAddress))
            return false;

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetAsync(serverAddress);
            // Any response means the server is running (even 401/403)
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false; // Timeout
        }
        catch (Exception)
        {
            return false;
        }
    }
}
