// <copyright file="NullEmbeddingProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain;

namespace Ouroboros.ApiHost;

/// <summary>
/// No-op embedding model used as a default when no real embedding model
/// is configured. Returns zero-vectors so CPE can still run (shift distances
/// will always be zero which means minimal resource cost).
/// </summary>
public sealed class NullEmbeddingProvider : IEmbeddingModel
{
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default) =>
        Task.FromResult(new float[384]);
}
