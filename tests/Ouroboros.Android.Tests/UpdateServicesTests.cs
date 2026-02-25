using FluentAssertions;
using Xunit;

namespace Ouroboros.Android.Tests;

/// <summary>
/// Tests for GitHub-based update mechanism.
/// These tests validate the update service logic without requiring MAUI dependencies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "UpdateServices")]
public class UpdateServicesTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("v1.0.1", "v1.0.0", true)]
    [InlineData("v1.0.0", "v1.0.1", false)]
    public void Version_Comparison_WorksCorrectly(string latestVersion, string currentVersion, bool expectedIsNewer)
    {
        // Arrange
        var latest = ParseVersion(latestVersion);
        var current = ParseVersion(currentVersion);

        // Act
        var result = latest > current;

        // Assert
        result.Should().Be(expectedIsNewer, 
            $"Version {latestVersion} should {(expectedIsNewer ? "be newer than" : "not be newer than")} {currentVersion}");
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v2.0.0", "2.0.0")]
    public void Version_Parsing_RemovesVPrefix(string input, string expected)
    {
        // Arrange & Act
        var result = ParseVersion(input);

        // Assert
        result.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(500L, "500 bytes")]
    [InlineData(2048L, "2.00 KB")]
    [InlineData(52428800L, "50.00 MB")]
    [InlineData(2147483648L, "2.00 GB")]
    public void FileSize_Formatting_WorksCorrectly(long bytes, string expected)
    {
        // Arrange & Act
        var result = FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void UpdateInfo_CanBeCreated()
    {
        // Arrange & Act
        var updateInfo = new
        {
            IsUpdateAvailable = true,
            CurrentVersion = "1.0.0",
            LatestVersion = "1.0.1",
            ReleaseNotes = "Bug fixes",
            DownloadUrl = "https://example.com/app.apk",
            FileSize = 50000000L,
            FormattedFileSize = "50.00 MB",
            ReleaseUrl = "https://github.com/test/releases/tag/v1.0.1"
        };

        // Assert
        updateInfo.Should().NotBeNull();
        updateInfo.IsUpdateAvailable.Should().BeTrue();
        updateInfo.CurrentVersion.Should().Be("1.0.0");
        updateInfo.LatestVersion.Should().Be("1.0.1");
        updateInfo.DownloadUrl.Should().Contain(".apk");
    }

    [Theory]
    [InlineData("app.apk", "application/vnd.android.package-archive", true)]
    [InlineData("app.APK", "application/vnd.android.package-archive", true)]
    [InlineData("source.zip", "application/zip", false)]
    [InlineData("readme.txt", "text/plain", false)]
    public void ApkAsset_Detection_WorksCorrectly(string fileName, string contentType, bool shouldBeApk)
    {
        // Arrange
        var isApk = fileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) &&
                    contentType == "application/vnd.android.package-archive";

        // Act & Assert
        isApk.Should().Be(shouldBeApk);
    }

    [Fact]
    public void GitHubRelease_FilteringLogic_Works()
    {
        // Arrange
        var releases = new[]
        {
            new { Draft = true, Prerelease = false, Version = "1.0.0" },
            new { Draft = false, Prerelease = true, Version = "1.0.1" },
            new { Draft = false, Prerelease = false, Version = "1.0.2" },
            new { Draft = false, Prerelease = false, Version = "1.0.3" }
        };

        // Act - Filter out drafts and prereleases
        var validReleases = releases.Where(r => !r.Draft && !r.Prerelease).ToList();

        // Assert
        validReleases.Should().HaveCount(2);
        validReleases.Should().OnlyContain(r => !r.Draft && !r.Prerelease);
    }

    [Fact]
    public void GitHubApi_EndpointConstruction_IsCorrect()
    {
        // Arrange
        const string owner = "PMeeske";
        const string repo = "ouroboros-app";
        const string baseUrl = "https://api.github.com";

        // Act
        var latestReleaseUrl = $"{baseUrl}/repos/{owner}/{repo}/releases/latest";
        var allReleasesUrl = $"{baseUrl}/repos/{owner}/{repo}/releases";
        var tagReleaseUrl = $"{baseUrl}/repos/{owner}/{repo}/releases/tags/v1.0.0";

        // Assert
        latestReleaseUrl.Should().Be("https://api.github.com/repos/PMeeske/ouroboros-app/releases/latest");
        allReleasesUrl.Should().Be("https://api.github.com/repos/PMeeske/ouroboros-app/releases");
        tagReleaseUrl.Should().Contain("/releases/tags/v1.0.0");
    }

    [Fact]
    public void FileProvider_Authority_IsConstructedCorrectly()
    {
        // Arrange
        const string packageName = "com.adaptivesystems.Ouroboros";

        // Act
        var authority = $"{packageName}.fileprovider";

        // Assert
        authority.Should().Be("com.adaptivesystems.Ouroboros.fileprovider");
    }

    // Helper methods matching the actual implementation
    private static Version ParseVersion(string versionString)
    {
        if (versionString.StartsWith('v') || versionString.StartsWith('V'))
        {
            versionString = versionString.Substring(1);
        }
        return Version.Parse(versionString);
    }

    private static string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (bytes >= GB)
        {
            return $"{bytes / (double)GB:F2} GB";
        }
        else if (bytes >= MB)
        {
            return $"{bytes / (double)MB:F2} MB";
        }
        else if (bytes >= KB)
        {
            return $"{bytes / (double)KB:F2} KB";
        }
        else
        {
            return $"{bytes} bytes";
        }
    }
}
