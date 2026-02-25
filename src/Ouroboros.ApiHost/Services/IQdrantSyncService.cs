// <copyright file="IQdrantSyncService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.ApiHost.Services;

/// <summary>
/// Service for managing Qdrant Cloud sync operations: status, diff, sync, verify,
/// collections listing, and encryption key info.
/// </summary>
public interface IQdrantSyncService
{
    /// <summary>
    /// Gets the connection health and encryption status of local and cloud Qdrant.
    /// </summary>
    Task<SyncStatusResponse> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Compares local vs cloud collections by name and point count.
    /// </summary>
    Task<SyncDiffResponse> GetDiffAsync(CancellationToken ct = default);

    /// <summary>
    /// Pushes collections from local to cloud with per-index EC encryption.
    /// </summary>
    /// <param name="collection">Optional collection name; null syncs all ouroboros collections.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SyncResultResponse> SyncAsync(string? collection = null, CancellationToken ct = default);

    /// <summary>
    /// Verifies the integrity of cloud vectors via HMAC-SHA256 check.
    /// </summary>
    /// <param name="collection">Optional collection name; null verifies all ouroboros collections.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SyncVerifyResponse> VerifyAsync(string? collection = null, CancellationToken ct = default);

    /// <summary>
    /// Lists all collections on the cloud cluster with point counts and dimensions.
    /// </summary>
    Task<SyncCollectionsResponse> ListCloudCollectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns EC encryption key metadata.
    /// </summary>
    SyncKeyInfoResponse? GetKeyInfo();
}
