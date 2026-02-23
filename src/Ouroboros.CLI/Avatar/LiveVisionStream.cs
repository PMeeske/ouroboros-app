// <copyright file="LiveVisionStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Avatar;

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// Captures avatar character assets, optionally generates new frames via
/// <see cref="AvatarVideoGenerator"/> (Stability AI cloud or local SD), sends them
/// to the Ollama vision model with streaming enabled, and relays both the JPEG frames
/// and streaming text tokens to avatar.html via <see cref="IVideoFrameRenderer"/>
/// and <see cref="IVisionTextRenderer"/>.
/// </summary>
public sealed class LiveVisionStream : IAsyncDisposable
{
    private readonly InteractiveAvatarService _avatarService;
    private readonly AvatarVideoGenerator? _frameGenerator;
    private readonly string _ollamaEndpoint;
    private readonly string _visionModel;
    private readonly string _assetDirectory;
    private readonly ILogger<LiveVisionStream>? _logger;
    private readonly HttpClient _httpClient;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Last generated frame — used as seed for the next iteration to create smooth animation.</summary>
    private byte[]? _lastGeneratedFrame;

    /// <summary>Visual state of the last generated frame — reset seed on state change.</summary>
    private AvatarVisualState _lastVisualState;

    /// <summary>Interval between frame captures in milliseconds.</summary>
    public int FrameIntervalMs { get; set; } = 5000;

    /// <summary>Prompt sent with each frame to the vision model.</summary>
    public string VisionPrompt { get; set; } =
        "You are observing a live avatar stream of an AI entity named Iaret. " +
        "Describe what you see in this frame — her expression, mood, posture, and any visual details. " +
        "Be poetic and concise. Stream your response naturally, one thought at a time.";

    /// <summary>Gets whether the stream loop is currently running.</summary>
    public bool IsRunning => _loopTask is { IsCompleted: false };

    public LiveVisionStream(
        InteractiveAvatarService avatarService,
        string ollamaEndpoint = "http://localhost:11434",
        string visionModel = "qwen3-vl:235b-cloud",
        string? assetDirectory = null,
        ILogger<LiveVisionStream>? logger = null,
        AvatarVideoGenerator? frameGenerator = null)
    {
        _avatarService = avatarService;
        _frameGenerator = frameGenerator;
        _ollamaEndpoint = ollamaEndpoint.TrimEnd('/');
        _visionModel = visionModel;
        _assetDirectory = assetDirectory ?? GetDefaultAssetDirectory();
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_ollamaEndpoint, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(180),
        };
    }

    /// <summary>Starts the live vision stream loop in the background.</summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        _logger?.LogInformation(
            "Live vision stream started — model={Model}, endpoint={Endpoint}, interval={Interval}ms",
            _visionModel, _ollamaEndpoint, FrameIntervalMs);
        return Task.CompletedTask;
    }

    /// <summary>Stops the stream loop.</summary>
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            if (_loopTask != null)
            {
                try { await _loopTask; }
                catch (OperationCanceledException) { }
            }
        }
        _logger?.LogInformation("Live vision stream stopped");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = _avatarService.CurrentState;

                // Determine seed: use previous generated frame for smooth animation,
                // reset to static asset only on first frame or visual state change.
                byte[] seedBytes;
                bool stateChanged = state.VisualState != _lastVisualState;

                if (_frameGenerator != null && _lastGeneratedFrame != null && !stateChanged)
                {
                    // Continuous animation: evolve from previous frame
                    seedBytes = _lastGeneratedFrame;
                }
                else
                {
                    // First frame or state change: load static asset as seed
                    var assetPath = AvatarVideoGenerator.GetSeedAssetPath(state.VisualState, _assetDirectory);
                    if (!File.Exists(assetPath))
                    {
                        _logger?.LogWarning("Asset not found: {Path}", assetPath);
                        await Task.Delay(FrameIntervalMs, ct);
                        continue;
                    }

                    seedBytes = await EncodeAsJpegAsync(assetPath, ct);
                    _lastVisualState = state.VisualState;
                }

                byte[] jpegBytes = seedBytes;

                // Generate a new frame via Stability AI / local SD if available
                if (_frameGenerator != null)
                {
                    var prompt = AvatarVideoGenerator.BuildPrompt(state);
                    string seedBase64 = Convert.ToBase64String(seedBytes);
                    string? generatedBase64 = await _frameGenerator.GenerateFrameAsync(prompt, seedBase64, state.VisualState, ct);

                    if (generatedBase64 != null)
                    {
                        jpegBytes = Convert.FromBase64String(generatedBase64);
                        _lastGeneratedFrame = jpegBytes;
                    }
                    else
                    {
                        _logger?.LogDebug("Frame generation returned null, using previous frame");
                    }
                }

                // Broadcast frame to avatar.html canvas
                await _avatarService.BroadcastVideoFrameAsync(jpegBytes);

                // Send to Ollama vision model with streaming — skip when frame generator
                // is active (the generated frames are the product, not text descriptions)
                if (_frameGenerator == null)
                {
                    await StreamVisionAnalysisAsync(jpegBytes, state, ct);
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in live vision stream loop");
            }

            await Task.Delay(FrameIntervalMs, ct);
        }
    }

    private async Task StreamVisionAnalysisAsync(byte[] jpegBytes, AvatarStateSnapshot state, CancellationToken ct)
    {
        string base64Image = Convert.ToBase64String(jpegBytes);

        var contextPrompt = $"{VisionPrompt}\n\n" +
                            $"Current state: {state.VisualState}, Mood: {state.Mood}, " +
                            $"Energy: {state.Energy:F2}, Persona: {state.PersonaName}";

        var requestBody = new
        {
            model = _visionModel,
            prompt = contextPrompt,
            images = new[] { base64Image },
            stream = true,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate") { Content = content };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("Ollama vision request failed ({Status}): {Error}", response.StatusCode, error);
            return;
        }

        // Signal new frame analysis to avatar.html (clears previous text)
        await BroadcastVisionTextToRenderersAsync($"[{state.VisualState} • {state.Mood}] ", isNewFrame: true);

        // Read streaming NDJSON response token by token
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var json = JsonDocument.Parse(line);
                if (json.RootElement.TryGetProperty("response", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        // Relay each token to avatar.html only (no CLI output)
                        await BroadcastVisionTextToRenderersAsync(token);
                    }
                }

                // Check if this is the final response
                if (json.RootElement.TryGetProperty("done", out var done) && done.GetBoolean())
                {
                    break;
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }
    }

    private async Task BroadcastVisionTextToRenderersAsync(string text, bool isNewFrame = false)
    {
        foreach (var renderer in GetVisionTextRenderers())
        {
            try
            {
                await renderer.BroadcastVisionTextAsync(text, isNewFrame);
            }
            catch (Exception)
            {
                // Individual renderer failures shouldn't crash the stream
            }
        }
    }

    private IEnumerable<IVisionTextRenderer> GetVisionTextRenderers()
    {
        // Access renderers through reflection on the avatar service's internal list.
        // The InteractiveAvatarService doesn't expose renderers publicly, so we use
        // a field accessor cached for performance.
        return _visionTextRenderers ??= ResolveVisionTextRenderers();
    }

    private List<IVisionTextRenderer>? _visionTextRenderers;

    private List<IVisionTextRenderer> ResolveVisionTextRenderers()
    {
        // Access _renderers field from InteractiveAvatarService
        var field = typeof(InteractiveAvatarService)
            .GetField("_renderers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(_avatarService) is List<IAvatarRenderer> renderers)
        {
            return renderers.OfType<IVisionTextRenderer>().ToList();
        }
        return [];
    }

    private static async Task<byte[]> EncodeAsJpegAsync(string pngPath, CancellationToken ct)
    {
        var rawBytes = await File.ReadAllBytesAsync(pngPath, ct);

        // If already JPEG, return as-is
        if (rawBytes.Length >= 2 && rawBytes[0] == 0xFF && rawBytes[1] == 0xD8)
            return rawBytes;

        // Convert PNG to JPEG using System.Drawing
        using var ms = new MemoryStream(rawBytes);
        using var bmp = new Bitmap(ms);
        using var outMs = new MemoryStream();
        bmp.Save(outMs, ImageFormat.Jpeg);
        return outMs.ToArray();
    }

    private static string GetDefaultAssetDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "Assets", "Avatar"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Ouroboros.CLI", "Assets", "Avatar"),
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(full)) return full;
        }

        return Path.Combine(baseDir, "Assets", "Avatar");
    }
}
