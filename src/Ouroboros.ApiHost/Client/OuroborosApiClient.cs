// <copyright file="OuroborosApiClient.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;
using Ouroboros.ApiHost.Models;

namespace Ouroboros.ApiHost.Client;

/// <summary>
/// Default <see cref="IOuroborosApiClient"/> implementation backed by
/// <see cref="IHttpClientFactory"/>.  Register via
/// <see cref="Extensions.WebApiServiceCollectionExtensions.AddOuroborosApiClient"/>.
/// </summary>
public sealed class OuroborosApiClient : IOuroborosApiClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _factory;

    public OuroborosApiClient(IHttpClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(
        string question,
        bool useRag = false,
        string? model = null,
        CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(OuroborosApiClientConstants.HttpClientName);

        var payload = new AskRequest
        {
            Question = question,
            UseRag = useRag,
            Model = model
        };

        HttpResponseMessage response = await http.PostAsJsonAsync("api/ask", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>(JsonOptions, ct);
        return result?.Data?.Answer
               ?? throw new InvalidOperationException("Upstream API returned an empty ask response.");
    }

    /// <inheritdoc/>
    public async Task<string> ExecutePipelineAsync(
        string dsl,
        string? model = null,
        CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(OuroborosApiClientConstants.HttpClientName);

        var payload = new PipelineRequest { Dsl = dsl, Model = model };

        HttpResponseMessage response = await http.PostAsJsonAsync("api/pipeline", payload, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>(JsonOptions, ct);
        return result?.Data?.Result
               ?? throw new InvalidOperationException("Upstream API returned an empty pipeline response.");
    }

    /// <inheritdoc/>
    public async Task<SelfStateResponse> GetSelfStateAsync(CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(OuroborosApiClientConstants.HttpClientName);

        HttpResponseMessage response = await http.GetAsync("api/self/state", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>(JsonOptions, ct);
        return result?.Data
               ?? throw new InvalidOperationException("Upstream API returned an empty self-state response.");
    }

    /// <inheritdoc/>
    public async Task<SelfForecastResponse> GetSelfForecastAsync(CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(OuroborosApiClientConstants.HttpClientName);

        HttpResponseMessage response = await http.GetAsync("api/self/forecast", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>(JsonOptions, ct);
        return result?.Data
               ?? throw new InvalidOperationException("Upstream API returned an empty forecast response.");
    }

    /// <inheritdoc/>
    public async Task<List<CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default)
    {
        using HttpClient http = _factory.CreateClient(OuroborosApiClientConstants.HttpClientName);

        HttpResponseMessage response = await http.GetAsync("api/self/commitments", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CommitmentDto>>>(JsonOptions, ct);
        return result?.Data
               ?? throw new InvalidOperationException("Upstream API returned an empty commitments response.");
    }
}
