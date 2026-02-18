using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Android.Services;

/// <summary>
/// Thin HTTP client that delegates all AI operations to the Ouroboros WebAPI.
/// The Android app acts as an upstream consumer — it does not run models locally.
/// </summary>
public sealed class OuroborosApiClient : IDisposable
{
    private readonly HttpClient _http;
    private string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosApiClient"/> class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Ouroboros WebAPI (e.g. http://192.168.1.100:5000)</param>
    public OuroborosApiClient(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    }

    /// <summary>
    /// Gets or sets the WebAPI base URL.
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = (value ?? "http://localhost:5000").TrimEnd('/');
    }

    // ── Ask ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ask a question via POST /api/ask.
    /// </summary>
    public async Task<AskResult> AskAsync(string question, bool useRag = false, CancellationToken ct = default)
    {
        var payload = new { question, useRag };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync($"{_baseUrl}/api/ask", content, ct);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AskData>>(JsonOptions, ct);

        if (apiResponse is { Success: true, Data: not null })
        {
            return new AskResult
            {
                Answer = apiResponse.Data.Answer ?? string.Empty,
                Model = apiResponse.Data.Model,
                ExecutionTimeMs = apiResponse.ExecutionTimeMs
            };
        }

        return new AskResult
        {
            Answer = $"Error: {apiResponse?.Error ?? "Unknown error from WebAPI"}"
        };
    }

    // ── Pipeline ───────────────────────────────────────────────────────

    /// <summary>
    /// Execute a DSL pipeline via POST /api/pipeline.
    /// </summary>
    public async Task<string> ExecutePipelineAsync(string dsl, CancellationToken ct = default)
    {
        var payload = new { dsl };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync($"{_baseUrl}/api/pipeline", content, ct);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineData>>(JsonOptions, ct);

        if (apiResponse is { Success: true, Data: not null })
            return apiResponse.Data.Result ?? string.Empty;

        return $"Error: {apiResponse?.Error ?? "Unknown error"}";
    }

    // ── Health / Status ────────────────────────────────────────────────

    /// <summary>
    /// Liveness check — GET /health.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fetch service info from the root endpoint — GET /.
    /// </summary>
    public async Task<string> GetServiceInfoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync(_baseUrl, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── Self-Model endpoints ───────────────────────────────────────────

    /// <summary>
    /// GET /api/self/state — agent identity state.
    /// </summary>
    public async Task<string> GetSelfStateAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/self/state", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public void Dispose() => _http.Dispose();

    // ── Response DTOs (minimal, matching WebAPI shape) ─────────────────

    private sealed class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
        public long? ExecutionTimeMs { get; set; }
    }

    private sealed class AskData
    {
        public string? Answer { get; set; }
        public string? Model { get; set; }
    }

    private sealed class PipelineData
    {
        public string? Result { get; set; }
        public string? FinalState { get; set; }
    }
}

/// <summary>
/// Result returned by <see cref="OuroborosApiClient.AskAsync"/>.
/// </summary>
public sealed class AskResult
{
    public string Answer { get; set; } = string.Empty;
    public string? Model { get; set; }
    public long? ExecutionTimeMs { get; set; }
}
