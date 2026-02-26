// <copyright file="NullEmbeddingProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.CognitivePhysics;

namespace Ouroboros.ApiHost;

/// <summary>
/// No-op embedding provider used as a default when no real embedding model
/// is configured. Returns zero-vectors so CPE can still run (shift distances
/// will always be zero which means minimal resource cost).
/// </summary>
#pragma warning disable CS0618 // Obsolete IEmbeddingProvider — CPE requires it
public sealed class NullEmbeddingProvider : IEmbeddingProvider
{
    public ValueTask<float[]> GetEmbeddingAsync(string text) =>
        ValueTask.FromResult(new float[384]);
}
