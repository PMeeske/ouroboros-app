// <copyright file="IOuroborosApiClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.ApiHost.Client;

/// <summary>
/// Typed HTTP client that lets the CLI (or Android app) delegate pipeline
/// execution to a remote Ouroboros Web API instance instead of running the
/// pipeline locally.  This makes the API a complete <em>upstream provider</em>
/// for any downstream host.
/// </summary>
public interface IOuroborosApiClient
{
    /// <summary>
    /// Asks a question via <c>POST /api/ask</c> on the upstream API.
    /// </summary>
    Task<string> AskAsync(string question, bool useRag = false, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a DSL pipeline via <c>POST /api/pipeline</c> on the upstream API.
    /// </summary>
    Task<string> ExecutePipelineAsync(string dsl, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the agent identity state via <c>GET /api/self/state</c>.
    /// </summary>
    Task<Models.SelfStateResponse> GetSelfStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns forecasts and anomalies via <c>GET /api/self/forecast</c>.
    /// </summary>
    Task<Models.SelfForecastResponse> GetSelfForecastAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns active commitments via <c>GET /api/self/commitments</c>.
    /// </summary>
    Task<List<Models.CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default);
}
