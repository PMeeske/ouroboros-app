using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class SyncModelsTests
{
    [Fact]
    public void SyncRequest_Collection_DefaultsToNull()
    {
        // Arrange & Act
        var request = new SyncRequest();

        // Assert
        request.Collection.Should().BeNull();
    }

    [Fact]
    public void SyncRequest_Collection_SetExplicitly_RetainsValue()
    {
        // Arrange & Act
        var request = new SyncRequest { Collection = "ouroboros_core" };

        // Assert
        request.Collection.Should().Be("ouroboros_core");
    }

    [Fact]
    public void EndpointStatus_Properties_SetAndGet()
    {
        // Arrange & Act
        var status = new EndpointStatus
        {
            Endpoint = "http://localhost:6333",
            Online = true,
            CollectionCount = 5
        };

        // Assert
        status.Endpoint.Should().Be("http://localhost:6333");
        status.Online.Should().BeTrue();
        status.CollectionCount.Should().Be(5);
    }

    [Fact]
    public void SyncStatusResponse_AllProperties_SetCorrectly()
    {
        // Arrange & Act
        var response = new SyncStatusResponse
        {
            Local = new EndpointStatus { Endpoint = "local", Online = true, CollectionCount = 3 },
            Cloud = new EndpointStatus { Endpoint = "cloud", Online = true, CollectionCount = 2 },
            EncryptionActive = true,
            EncryptionCurve = "NIST P-256",
            Ready = true
        };

        // Assert
        response.Local.Online.Should().BeTrue();
        response.Cloud.Online.Should().BeTrue();
        response.EncryptionActive.Should().BeTrue();
        response.EncryptionCurve.Should().Be("NIST P-256");
        response.Ready.Should().BeTrue();
    }

    [Fact]
    public void CollectionDiff_Properties_SetAndGet()
    {
        // Arrange & Act
        var diff = new CollectionDiff
        {
            Name = "ouroboros_core",
            LocalPoints = 100,
            LocalDimension = 384,
            CloudPoints = 95,
            CloudDimension = 384,
            Status = "diverged"
        };

        // Assert
        diff.Name.Should().Be("ouroboros_core");
        diff.LocalPoints.Should().Be(100);
        diff.CloudPoints.Should().Be(95);
        diff.Status.Should().Be("diverged");
    }

    [Fact]
    public void SyncDiffResponse_Properties_SetAndGet()
    {
        // Arrange & Act
        var response = new SyncDiffResponse
        {
            Collections = new List<CollectionDiff>(),
            Synced = 3,
            Diverged = 1,
            LocalOnly = 2,
            CloudOnly = 0
        };

        // Assert
        response.Synced.Should().Be(3);
        response.Diverged.Should().Be(1);
        response.LocalOnly.Should().Be(2);
        response.CloudOnly.Should().Be(0);
    }

    [Fact]
    public void CollectionSyncResult_Properties_SetAndGet()
    {
        // Arrange & Act
        var result = new CollectionSyncResult
        {
            Name = "test_collection",
            Points = 500,
            Dimension = 768,
            Synced = 495,
            Failed = 5,
            Error = null
        };

        // Assert
        result.Name.Should().Be("test_collection");
        result.Points.Should().Be(500);
        result.Synced.Should().Be(495);
        result.Failed.Should().Be(5);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SyncResultResponse_Properties_SetAndGet()
    {
        // Arrange & Act
        var response = new SyncResultResponse
        {
            Collections = new List<CollectionSyncResult>(),
            TotalSynced = 1000,
            TotalFailed = 10
        };

        // Assert
        response.TotalSynced.Should().Be(1000);
        response.TotalFailed.Should().Be(10);
    }

    [Fact]
    public void CollectionVerifyResult_Properties_SetAndGet()
    {
        // Arrange & Act
        var result = new CollectionVerifyResult
        {
            Name = "ouroboros_core",
            Points = 200,
            Intact = 195,
            Corrupted = 3,
            MissingHmac = 2,
            Error = null
        };

        // Assert
        result.Intact.Should().Be(195);
        result.Corrupted.Should().Be(3);
        result.MissingHmac.Should().Be(2);
    }

    [Fact]
    public void SyncVerifyResponse_Properties_SetAndGet()
    {
        // Arrange & Act
        var response = new SyncVerifyResponse
        {
            Collections = new List<CollectionVerifyResult>(),
            TotalIntact = 900,
            TotalCorrupted = 5,
            TotalMissingHmac = 10
        };

        // Assert
        response.TotalIntact.Should().Be(900);
        response.TotalCorrupted.Should().Be(5);
        response.TotalMissingHmac.Should().Be(10);
    }

    [Fact]
    public void SyncCollectionsResponse_Properties_SetAndGet()
    {
        // Arrange & Act
        var collections = new List<CloudCollectionInfo>
        {
            new() { Name = "core", Points = 100, Dimension = 384 }
        };
        var response = new SyncCollectionsResponse
        {
            Collections = collections,
            TotalCollections = 1,
            TotalPoints = 100
        };

        // Assert
        response.Collections.Should().HaveCount(1);
        response.TotalCollections.Should().Be(1);
        response.TotalPoints.Should().Be(100);
    }

    [Fact]
    public void CloudCollectionInfo_Properties_SetAndGet()
    {
        // Arrange & Act
        var info = new CloudCollectionInfo
        {
            Name = "embeddings",
            Points = 5000,
            Dimension = 1536
        };

        // Assert
        info.Name.Should().Be("embeddings");
        info.Points.Should().Be(5000);
        info.Dimension.Should().Be(1536);
    }

    [Fact]
    public void SyncKeyInfoResponse_Properties_SetAndGet()
    {
        // Arrange & Act
        var response = new SyncKeyInfoResponse
        {
            Curve = "NIST P-256 (secp256r1)",
            Mode = "Per-index keystream",
            PublicKeyFingerprint = "abc...xyz",
            FullPublicKey = "MFkwEwYHKo..."
        };

        // Assert
        response.Curve.Should().Contain("P-256");
        response.Mode.Should().Contain("Per-index");
        response.PublicKeyFingerprint.Should().NotBeEmpty();
        response.FullPublicKey.Should().NotBeEmpty();
    }
}
