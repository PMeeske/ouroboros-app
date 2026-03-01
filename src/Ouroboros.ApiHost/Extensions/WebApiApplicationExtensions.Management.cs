// <copyright file="WebApiApplicationExtensions.Management.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Ouroboros.ApiHost.Extensions;

public static partial class WebApiApplicationExtensions
{
    /// <summary>
    /// Maps management API endpoints for Qdrant Cloud sync operations.
    /// </summary>
    private static void MapManagementEndpoints(WebApplication app)
    {
        app.MapGet("/api/manage/sync/status", async (IQdrantSyncService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SyncStatusResponse status = await service.GetStatusAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SyncStatusResponse>.Ok(status, sw.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(ApiResponse<SyncStatusResponse>.Fail($"Error: {ex.Message}"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<SyncStatusResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("GetSyncStatus")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "Get Qdrant Cloud sync status";
            op.Description = "Returns connection health for local and cloud Qdrant, plus encryption status.";
            return op;
        })
        .Produces<ApiResponse<SyncStatusResponse>>(200)
        .Produces<ApiResponse<SyncStatusResponse>>(400);

        app.MapGet("/api/manage/sync/diff", async (IQdrantSyncService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SyncDiffResponse diff = await service.GetDiffAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SyncDiffResponse>.Ok(diff, sw.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(ApiResponse<SyncDiffResponse>.Fail($"Error: {ex.Message}"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<SyncDiffResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("GetSyncDiff")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "Compare local vs cloud collections";
            op.Description = "Diffs collection names and point counts between local Qdrant and Qdrant Cloud.";
            return op;
        })
        .Produces<ApiResponse<SyncDiffResponse>>(200)
        .Produces<ApiResponse<SyncDiffResponse>>(400);

        app.MapPost("/api/manage/sync", async (SyncRequest? request, IQdrantSyncService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SyncResultResponse result = await service.SyncAsync(request?.Collection, ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SyncResultResponse>.Ok(result, sw.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(ApiResponse<SyncResultResponse>.Fail($"Error: {ex.Message}"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<SyncResultResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("SyncToCloud")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "Sync local collections to Qdrant Cloud";
            op.Description = "Pushes all ouroboros collections (or a specific one) to cloud with per-index EC encryption.";
            return op;
        })
        .Produces<ApiResponse<SyncResultResponse>>(200)
        .Produces<ApiResponse<SyncResultResponse>>(400);

        app.MapPost("/api/manage/sync/verify", async (SyncRequest? request, IQdrantSyncService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SyncVerifyResponse result = await service.VerifyAsync(request?.Collection, ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SyncVerifyResponse>.Ok(result, sw.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(ApiResponse<SyncVerifyResponse>.Fail($"Error: {ex.Message}"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<SyncVerifyResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("VerifyCloudIntegrity")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "Verify integrity of cloud vectors";
            op.Description = "Decrypts cloud vectors and verifies HMAC-SHA256 integrity. Reports intact, corrupted, and missing-HMAC counts.";
            return op;
        })
        .Produces<ApiResponse<SyncVerifyResponse>>(200)
        .Produces<ApiResponse<SyncVerifyResponse>>(400);

        app.MapGet("/api/manage/sync/collections", async (IQdrantSyncService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SyncCollectionsResponse result = await service.ListCloudCollectionsAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SyncCollectionsResponse>.Ok(result, sw.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(ApiResponse<SyncCollectionsResponse>.Fail($"Error: {ex.Message}"));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<SyncCollectionsResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("ListCloudCollections")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "List cloud collections";
            op.Description = "Returns all collections on the Qdrant Cloud cluster with point counts and dimensions.";
            return op;
        })
        .Produces<ApiResponse<SyncCollectionsResponse>>(200)
        .Produces<ApiResponse<SyncCollectionsResponse>>(400);

        app.MapGet("/api/manage/sync/keyinfo", (IQdrantSyncService service) =>
        {
            var info = service.GetKeyInfo();
            return info != null
                ? Results.Ok(ApiResponse<SyncKeyInfoResponse>.Ok(info))
                : Results.BadRequest(ApiResponse<SyncKeyInfoResponse>.Fail("No encryption key loaded."));
        })
        .WithName("GetSyncKeyInfo")
        .WithTags("Management")
        .WithOpenApi(op =>
        {
            op.Summary = "Get EC encryption key info";
            op.Description = "Returns the curve, mode, and public key fingerprint of the EC key used for vector encryption.";
            return op;
        })
        .Produces<ApiResponse<SyncKeyInfoResponse>>(200)
        .Produces<ApiResponse<SyncKeyInfoResponse>>(400);
    }
}
