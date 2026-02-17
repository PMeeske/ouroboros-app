// <copyright file="SelfModelRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.WebApi.Models;

/// <summary>
/// Request for self-model explain endpoint.
/// </summary>
public sealed record SelfExplainRequest
{
    /// <summary>
    /// Gets or sets the event ID or range to explain.
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Gets or sets whether to include full DAG context.
    /// </summary>
    public bool IncludeContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum depth for narrative generation.
    /// </summary>
    public int MaxDepth { get; set; } = 5;
}