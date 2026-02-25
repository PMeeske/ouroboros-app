// <copyright file="SyncModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.ApiHost.Models;

/// <summary>
/// Request body for sync and verify operations.
/// </summary>
public sealed record SyncRequest
{
    /// <summary>
    /// Optional collection name. If null, operates on all ouroboros collections.
    /// </summary>
    public string? Collection { get; init; }
}

/// <summary>
/// Connection health and encryption status for the sync subsystem.
/// </summary>
public sealed record SyncStatusResponse
{
    public required EndpointStatus Local { get; init; }
    public required EndpointStatus Cloud { get; init; }
    public bool EncryptionActive { get; init; }
    public string? EncryptionCurve { get; init; }
    public bool Ready { get; init; }
}

/// <summary>
/// Health status of a single Qdrant endpoint.
/// </summary>
public sealed record EndpointStatus
{
    public required string Endpoint { get; init; }
    public bool Online { get; init; }
    public int CollectionCount { get; init; }
}

/// <summary>
/// Local vs cloud collection comparison.
/// </summary>
public sealed record SyncDiffResponse
{
    public required List<CollectionDiff> Collections { get; init; }
    public int Synced { get; init; }
    public int Diverged { get; init; }
    public int LocalOnly { get; init; }
    public int CloudOnly { get; init; }
}

/// <summary>
/// Diff for a single collection.
/// </summary>
public sealed record CollectionDiff
{
    public required string Name { get; init; }
    public int? LocalPoints { get; init; }
    public int? LocalDimension { get; init; }
    public int? CloudPoints { get; init; }
    public int? CloudDimension { get; init; }
    public required string Status { get; init; }
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public sealed record SyncResultResponse
{
    public required List<CollectionSyncResult> Collections { get; init; }
    public int TotalSynced { get; init; }
    public int TotalFailed { get; init; }
}

/// <summary>
/// Sync result for a single collection.
/// </summary>
public sealed record CollectionSyncResult
{
    public required string Name { get; init; }
    public int Points { get; init; }
    public int Dimension { get; init; }
    public int Synced { get; init; }
    public int Failed { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Result of an integrity verification operation.
/// </summary>
public sealed record SyncVerifyResponse
{
    public required List<CollectionVerifyResult> Collections { get; init; }
    public int TotalIntact { get; init; }
    public int TotalCorrupted { get; init; }
    public int TotalMissingHmac { get; init; }
}

/// <summary>
/// Verification result for a single collection.
/// </summary>
public sealed record CollectionVerifyResult
{
    public required string Name { get; init; }
    public int Points { get; init; }
    public int Intact { get; init; }
    public int Corrupted { get; init; }
    public int MissingHmac { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Cloud collections listing.
/// </summary>
public sealed record SyncCollectionsResponse
{
    public required List<CloudCollectionInfo> Collections { get; init; }
    public int TotalCollections { get; init; }
    public long TotalPoints { get; init; }
}

/// <summary>
/// Info about a single cloud collection.
/// </summary>
public sealed record CloudCollectionInfo
{
    public required string Name { get; init; }
    public int Points { get; init; }
    public int Dimension { get; init; }
}

/// <summary>
/// EC encryption key information.
/// </summary>
public sealed record SyncKeyInfoResponse
{
    public required string Curve { get; init; }
    public required string Mode { get; init; }
    public required string PublicKeyFingerprint { get; init; }
    public required string FullPublicKey { get; init; }
}
