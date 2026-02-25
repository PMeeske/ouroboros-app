// <copyright file="ConversationMemory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// A single conversation turn.
/// </summary>
public sealed record ConversationTurn(
    string Role,
    string Content,
    DateTime Timestamp,
    string? SessionId = null,
    Dictionary<string, string>? Metadata = null);