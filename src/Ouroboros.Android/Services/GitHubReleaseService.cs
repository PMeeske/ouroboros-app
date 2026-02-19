using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Android.Services;

/// <summary>
/// Service for fetching release information from GitHub.
/// </summary>
public class GitHubReleaseService
{
    private readonly HttpClient _httpClient;
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private const string RepositoryOwner = "PMeeske";
    private const string RepositoryName = "ouroboros-app";

    public GitHubReleaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Ouroboros-Android-App");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    /// <summary>
    /// Gets the latest release from GitHub.
    /// </summary>
    /// <returns>Latest release information or null if not found.</returns>
    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url, cancellationToken);
            return release;
        }
        catch (HttpRequestException ex)
        {
            // Handle 404 (no releases) gracefully
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            throw;
        }
    }

    /// <summary>
    /// Gets all releases from GitHub (paginated).
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="perPage">Number of releases per page (max 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of releases.</returns>
    public async Task<List<GitHubRelease>> GetReleasesAsync(int page = 1, int perPage = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases?page={page}&per_page={perPage}";
            var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(url, cancellationToken);
            return releases ?? new List<GitHubRelease>();
        }
        catch (HttpRequestException)
        {
            return new List<GitHubRelease>();
        }
    }

    /// <summary>
    /// Gets a specific release by tag name.
    /// </summary>
    /// <param name="tagName">Tag name (e.g., "v1.0.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Release information or null if not found.</returns>
    public async Task<GitHubRelease?> GetReleaseByTagAsync(string tagName, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/tags/{tagName}";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url, cancellationToken);
            return release;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            throw;
        }
    }
}

/// <summary>
/// Represents a GitHub release.
/// </summary>
public class GitHubRelease
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = new();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets the APK asset from the release, if available.
    /// </summary>
    public GitHubReleaseAsset? GetApkAsset()
    {
        return Assets.FirstOrDefault(a => 
            a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) &&
            a.ContentType == "application/vnd.android.package-archive");
    }

    /// <summary>
    /// Gets the version from the tag name (strips 'v' prefix if present).
    /// </summary>
    public string GetVersion()
    {
        var version = TagName;
        if (version.StartsWith('v') || version.StartsWith('V'))
        {
            version = version.Substring(1);
        }
        return version;
    }
}

/// <summary>
/// Represents a GitHub release asset (file).
/// </summary>
public class GitHubReleaseAsset
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets a human-readable file size string.
    /// </summary>
    public string GetFormattedSize()
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        if (Size >= GB)
        {
            return $"{Size / (double)GB:F2} GB";
        }
        else if (Size >= MB)
        {
            return $"{Size / (double)MB:F2} MB";
        }
        else if (Size >= KB)
        {
            return $"{Size / (double)KB:F2} KB";
        }
        else
        {
            return $"{Size} bytes";
        }
    }
}
