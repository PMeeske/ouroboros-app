using System.IO;
using System.Net.Http;

namespace Ouroboros.Android.Services;

/// <summary>
/// Service for managing app updates from GitHub releases.
/// </summary>
public class UpdateManagerService
{
    private readonly GitHubReleaseService _githubService;
    private readonly HttpClient _httpClient;
    private string _currentVersion;

    public UpdateManagerService(GitHubReleaseService githubService, HttpClient httpClient)
    {
        _githubService = githubService;
        _httpClient = httpClient;
        _currentVersion = AppInfo.VersionString;
    }

    /// <summary>
    /// Checks if an update is available.
    /// </summary>
    /// <returns>Update information if available, null otherwise.</returns>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestRelease = await _githubService.GetLatestReleaseAsync(cancellationToken);
            
            if (latestRelease == null)
            {
                return null;
            }

            // Skip draft and prerelease versions
            if (latestRelease.Draft || latestRelease.Prerelease)
            {
                return null;
            }

            // Check if APK asset exists
            var apkAsset = latestRelease.GetApkAsset();
            if (apkAsset == null)
            {
                return null;
            }

            var latestVersion = latestRelease.GetVersion();
            var isNewer = IsVersionNewer(latestVersion, _currentVersion);

            return new UpdateInfo
            {
                IsUpdateAvailable = isNewer,
                CurrentVersion = _currentVersion,
                LatestVersion = latestVersion,
                ReleaseNotes = latestRelease.Body,
                DownloadUrl = apkAsset.BrowserDownloadUrl,
                FileSize = apkAsset.Size,
                FormattedFileSize = apkAsset.GetFormattedSize(),
                ReleaseUrl = latestRelease.HtmlUrl,
                PublishedAt = latestRelease.PublishedAt
            };
        }
        catch (HttpRequestException ex)
        {
            // Log error but don't throw - return null to indicate no update available
            System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the APK file from the given URL.
    /// </summary>
    /// <param name="downloadUrl">URL to download the APK from.</param>
    /// <param name="progress">Progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the downloaded APK file.</returns>
    public async Task<string> DownloadUpdateAsync(
        string downloadUrl, 
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            // Save to cache directory
            var fileName = $"update-{DateTime.Now:yyyyMMddHHmmss}.apk";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = File.Create(filePath);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var progressValue = (double)downloadedBytes / totalBytes;
                    progress.Report(progressValue);
                }
            }

            return filePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading update: {ex.Message}");
            throw;
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading update: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Installs the downloaded APK file.
    /// </summary>
    /// <param name="apkPath">Path to the APK file.</param>
    public void InstallUpdate(string apkPath)
    {
#if ANDROID
        var file = new Java.IO.File(apkPath);
        
        if (!file.Exists())
        {
            throw new FileNotFoundException("APK file not found", apkPath);
        }

        var context = Platform.CurrentActivity ?? throw new InvalidOperationException("Current activity is null");
        var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
        
        // Use FileProvider for Android 7.0+ (API 24+)
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
        {
            var authority = $"{context.PackageName}.fileprovider";
            var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, file);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
        }
        else
        {
            intent.SetDataAndType(Android.Net.Uri.FromFile(file), "application/vnd.android.package-archive");
        }
        
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
#else
        throw new PlatformNotSupportedException("APK installation is only supported on Android");
#endif
    }

    /// <summary>
    /// Compares two version strings to determine if the first is newer than the second.
    /// </summary>
    private bool IsVersionNewer(string version1, string version2)
    {
        try
        {
            // Parse versions - handle both "1.0.0" and "v1.0.0" formats
            var v1 = ParseVersion(version1);
            var v2 = ParseVersion(version2);

            return v1 > v2;
        }
        catch
        {
            // If parsing fails, do string comparison
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }

    /// <summary>
    /// Parses a version string into a Version object.
    /// </summary>
    private Version ParseVersion(string versionString)
    {
        // Remove 'v' or 'V' prefix if present
        if (versionString.StartsWith('v') || versionString.StartsWith('V'))
        {
            versionString = versionString.Substring(1);
        }

        // Parse the version
        return Version.Parse(versionString);
    }

    /// <summary>
    /// Cleans up old downloaded APK files from cache.
    /// </summary>
    public void CleanupOldUpdates()
    {
        try
        {
            var cacheDir = FileSystem.CacheDirectory;
            var apkFiles = Directory.GetFiles(cacheDir, "update-*.apk");

            foreach (var file in apkFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FormattedFileSize { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}
