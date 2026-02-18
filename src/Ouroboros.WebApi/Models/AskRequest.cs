// <copyright file="AskRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.WebApi.Models;

/// <summary>
/// Request model for asking questions to the AI pipeline.
/// </summary>
public sealed record AskRequest
{
    /// <summary>
    /// The question or prompt to ask. Example: "What is functional programming?"
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Enable Retrieval Augmented Generation (RAG). When true, relevant context is
    /// retrieved from <see cref="SourcePath"/> and included with the question for
    /// more grounded answers.
    /// </summary>
    public bool UseRag { get; init; }

    /// <summary>
    /// Directory or file path used as context when <see cref="UseRag"/> is true.
    /// Only relevant on the server's filesystem. Leave null to skip RAG.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Model name for generation. Defaults to "llama3" when omitted.
    /// Must match a model available on the configured provider (e.g. "llama3", "phi3:mini").
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Enable agent mode. The model can call tools (math, search, code execution)
    /// for multi-step reasoning over complex questions.
    /// </summary>
    public bool Agent { get; init; }

    /// <summary>
    /// Sampling temperature. Range: 0.0 (deterministic) to 2.0 (creative). Default: 0.7.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens in the response. Typical range: 100 - 8000.
    /// Higher values allow longer answers but increase latency.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Remote LLM endpoint URL to override the server default.
    /// Example: "https://api.openai.com/v1" or "http://localhost:11434".
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for the remote endpoint. Required for cloud providers; not needed for local Ollama.
    /// </summary>
    public string? ApiKey { get; init; }
}
