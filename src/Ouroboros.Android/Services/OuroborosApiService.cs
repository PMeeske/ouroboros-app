// <copyright file="OuroborosApiService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;

namespace Ouroboros.Android.Services;

/// <summary>
/// Lightweight service that connects the MAUI Android app to a running Ouroboros
/// Web API instance (standalone, CLI <c>--serve</c>, or any reachable host).
///
/// This is the Android side of the co-hosting contract: the Android app delegates
/// all AI pipeline work to the API rather than running LLM inference locally,
/// keeping the APK small and offloading heavy computation to a server.
/// </summary>
public sealed class OuroborosApiService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _factory;

    /// <summary>Named <see cref="IHttpClientFactory"/> key used by <see cref="MauiProgram"/>.</summary>
    public const string HttpClientName = "OuroborosApi";

    public OuroborosApiService(IHttpClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Asks a question via <c>POST /api/ask</c> on the upstream Ouroboros API.
    /// </summary>
    public async Task<string> AskAsync(
        string question,
        bool useRag = false,
        string? model = null,
        CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(HttpClientName);

        var payload = new { question, useRag, model };
        HttpResponseMessage response = await http.PostAsJsonAsync("api/ask", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct)
                                 ?? throw new InvalidOperationException("Empty response from API.");

        return doc.RootElement
                  .GetProperty("data")
                  .GetProperty("answer")
                  .GetString()
               ?? string.Empty;
    }

    /// <summary>
    /// Executes a DSL pipeline via <c>POST /api/pipeline</c> on the upstream Ouroboros API.
    /// </summary>
    public async Task<string> ExecutePipelineAsync(
        string dsl,
        string? model = null,
        CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(HttpClientName);

        var payload = new { dsl, model };
        HttpResponseMessage response = await http.PostAsJsonAsync("api/pipeline", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct)
                                 ?? throw new InvalidOperationException("Empty response from API.");

        return doc.RootElement
                  .GetProperty("data")
                  .GetProperty("result")
                  .GetString()
               ?? string.Empty;
    }

    /// <summary>
    /// Checks that the upstream API is reachable via <c>GET /health</c>.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpClient http = _factory.CreateClient(HttpClientName);
            HttpResponseMessage response = await http.GetAsync("health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
