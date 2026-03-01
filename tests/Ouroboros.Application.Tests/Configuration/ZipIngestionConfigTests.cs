// <copyright file="ZipIngestionConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class ZipIngestionConfigTests
{
    // --- Defaults ---

    [Fact]
    public void Constructor_WithRequiredArchivePath_ShouldSetArchivePath()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "/data/archive.zip" };

        // Assert
        config.ArchivePath.Should().Be("/data/archive.zip");
    }

    [Fact]
    public void IncludeXmlText_Default_ShouldBeTrue()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.IncludeXmlText.Should().BeTrue();
    }

    [Fact]
    public void CsvMaxLines_Default_ShouldMatchDefaultIngestionSettings()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.CsvMaxLines.Should().Be(DefaultIngestionSettings.CsvMaxLines);
    }

    [Fact]
    public void BinaryMaxBytes_Default_ShouldMatchDefaultIngestionSettings()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.BinaryMaxBytes.Should().Be(DefaultIngestionSettings.BinaryMaxBytes);
    }

    [Fact]
    public void MaxTotalBytes_Default_ShouldMatchDefaultIngestionSettings()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.MaxTotalBytes.Should().Be(DefaultIngestionSettings.MaxArchiveSizeBytes);
    }

    [Fact]
    public void MaxCompressionRatio_Default_ShouldMatchDefaultIngestionSettings()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.MaxCompressionRatio.Should().Be(DefaultIngestionSettings.MaxCompressionRatio);
    }

    [Fact]
    public void SkipKinds_Default_ShouldBeNull()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.SkipKinds.Should().BeNull();
    }

    [Fact]
    public void OnlyKinds_Default_ShouldBeNull()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.OnlyKinds.Should().BeNull();
    }

    [Fact]
    public void NoEmbed_Default_ShouldBeFalse()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.NoEmbed.Should().BeFalse();
    }

    [Fact]
    public void BatchSize_Default_ShouldMatchDefaultIngestionSettings()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig { ArchivePath = "test.zip" };

        // Assert
        config.BatchSize.Should().Be(DefaultIngestionSettings.DefaultBatchSize);
    }

    // --- Custom Values ---

    [Fact]
    public void AllProperties_ShouldBeSettable()
    {
        // Arrange & Act
        var config = new ZipIngestionConfig
        {
            ArchivePath = "/data/my-archive.zip",
            IncludeXmlText = false,
            CsvMaxLines = 100,
            BinaryMaxBytes = 256 * 1024,
            MaxTotalBytes = 1024 * 1024 * 1024,
            MaxCompressionRatio = 50.0,
            SkipKinds = new HashSet<string> { "binary", "image" },
            OnlyKinds = new HashSet<string> { "text", "csv" },
            NoEmbed = true,
            BatchSize = 32
        };

        // Assert
        config.ArchivePath.Should().Be("/data/my-archive.zip");
        config.IncludeXmlText.Should().BeFalse();
        config.CsvMaxLines.Should().Be(100);
        config.BinaryMaxBytes.Should().Be(256 * 1024);
        config.MaxTotalBytes.Should().Be(1024 * 1024 * 1024);
        config.MaxCompressionRatio.Should().Be(50.0);
        config.SkipKinds.Should().Contain("binary").And.Contain("image");
        config.OnlyKinds.Should().Contain("text").And.Contain("csv");
        config.NoEmbed.Should().BeTrue();
        config.BatchSize.Should().Be(32);
    }

    // --- Record Semantics ---

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new ZipIngestionConfig { ArchivePath = "test.zip", CsvMaxLines = 25 };
        var b = new ZipIngestionConfig { ArchivePath = "test.zip", CsvMaxLines = 25 };

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = new ZipIngestionConfig { ArchivePath = "test.zip", CsvMaxLines = 25 };
        var b = new ZipIngestionConfig { ArchivePath = "test.zip", CsvMaxLines = 50 };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new ZipIngestionConfig
        {
            ArchivePath = "test.zip",
            BatchSize = 16,
            NoEmbed = false
        };

        // Act
        var modified = original with { BatchSize = 64, NoEmbed = true };

        // Assert
        modified.ArchivePath.Should().Be("test.zip");
        modified.BatchSize.Should().Be(64);
        modified.NoEmbed.Should().BeTrue();
        original.BatchSize.Should().Be(16, "original should not be changed");
    }
}
