// <copyright file="PipelineRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.ApiHost.Models;

/// <summary>
/// Request model for executing pipelines.
/// </summary>
public sealed record PipelineRequest
{
    /// <summary>
    /// DSL expression describing the pipeline steps.
    /// Example: "SetTopic('AI') | UseDraft | UseCritique | UseImprove".
    /// Pipe-separated operations are executed left to right.
    /// </summary>
    public required string Dsl { get; init; }

    /// <summary>
    /// Model name for generation. Defaults to "llama3" when omitted.
    /// Must match a model available on the configured provider.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Enable debug output. When true, intermediate pipeline states and
    /// timing information are included in the response.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Sampling temperature. Range: 0.0 (deterministic) to 2.0 (creative). Default: 0.7.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens in the response. Typical range: 100 - 8000.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Remote LLM endpoint URL to override the server default.
    /// Example: "http://localhost:11434".
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for the remote endpoint. Required for cloud providers; not needed for local Ollama.
    /// </summary>
    public string? ApiKey { get; init; }
}
