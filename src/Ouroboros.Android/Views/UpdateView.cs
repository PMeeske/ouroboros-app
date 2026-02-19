using Microsoft.Maui.Controls;
using Ouroboros.Android.Services;

namespace Ouroboros.Android.Views;

/// <summary>
/// View for checking and installing app updates from GitHub.
/// </summary>
public class UpdateView : ContentPage
{
    private readonly UpdateManagerService _updateManager;
    private readonly Label _statusLabel;
    private readonly Label _currentVersionLabel;
    private readonly Label _latestVersionLabel;
    private readonly Label _releaseDateLabel;
    private readonly Label _fileSizeLabel;
    private readonly Editor _releaseNotesEditor;
    private readonly Button _checkButton;
    private readonly Button _downloadButton;
    private readonly Button _viewReleaseButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressLabel;
    private UpdateInfo? _currentUpdateInfo;

    public UpdateView(UpdateManagerService updateManager)
    {
        _updateManager = updateManager;
        
        Title = "App Updates";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        // Status label
        _statusLabel = new Label
        {
            Text = "Check for updates to get the latest features and improvements.",
            TextColor = Color.FromRgb(200, 200, 200),
            FontSize = 14,
            Margin = new Thickness(10, 20, 10, 10),
            HorizontalTextAlignment = TextAlignment.Center
        };

        // Current version
        var currentVersionTitleLabel = new Label
        {
            Text = "Current Version:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 10, 10, 5)
        };

        _currentVersionLabel = new Label
        {
            Text = AppInfo.VersionString,
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(20, 0, 10, 10)
        };

        // Latest version
        var latestVersionTitleLabel = new Label
        {
            Text = "Latest Version:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 10, 10, 5),
            IsVisible = false
        };

        _latestVersionLabel = new Label
        {
            Text = "Unknown",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(20, 0, 10, 10),
            IsVisible = false
        };

        // Release date
        _releaseDateLabel = new Label
        {
            TextColor = Color.FromRgb(150, 150, 150),
            FontSize = 12,
            Margin = new Thickness(20, 0, 10, 5),
            IsVisible = false
        };

        // File size
        _fileSizeLabel = new Label
        {
            TextColor = Color.FromRgb(150, 150, 150),
            FontSize = 12,
            Margin = new Thickness(20, 0, 10, 10),
            IsVisible = false
        };

        // Release notes
        var releaseNotesTitleLabel = new Label
        {
            Text = "Release Notes:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 10, 10, 5),
            IsVisible = false
        };

        _releaseNotesEditor = new Editor
        {
            IsReadOnly = true,
            TextColor = Color.FromRgb(200, 200, 200),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            FontFamily = "Courier New",
            FontSize = 12,
            Margin = new Thickness(10, 0, 10, 10),
            HeightRequest = 200,
            IsVisible = false
        };

        // Progress bar
        _progressBar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Color.FromRgb(0, 170, 0),
            Margin = new Thickness(10, 10, 10, 5),
            IsVisible = false
        };

        _progressLabel = new Label
        {
            Text = "Downloading...",
            TextColor = Color.FromRgb(0, 255, 0),
            FontSize = 12,
            Margin = new Thickness(10, 0, 10, 10),
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };

        // Buttons
        _checkButton = new Button
        {
            Text = "Check for Updates",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10, 10, 10, 5)
        };
        _checkButton.Clicked += OnCheckForUpdatesClicked;

        _downloadButton = new Button
        {
            Text = "Download Update",
            BackgroundColor = Color.FromRgb(0, 100, 170),
            TextColor = Colors.White,
            Margin = new Thickness(10, 5, 10, 5),
            IsVisible = false
        };
        _downloadButton.Clicked += OnDownloadUpdateClicked;

        _viewReleaseButton = new Button
        {
            Text = "View Release on GitHub",
            BackgroundColor = Color.FromRgb(60, 60, 60),
            TextColor = Colors.White,
            Margin = new Thickness(10, 5, 10, 10),
            IsVisible = false
        };
        _viewReleaseButton.Clicked += OnViewReleaseClicked;

        // Layout
        var scrollView = new ScrollView
        {
            Content = new StackLayout
            {
                Children =
                {
                    _statusLabel,
                    currentVersionTitleLabel,
                    _currentVersionLabel,
                    latestVersionTitleLabel,
                    _latestVersionLabel,
                    _releaseDateLabel,
                    _fileSizeLabel,
                    releaseNotesTitleLabel,
                    _releaseNotesEditor,
                    _progressBar,
                    _progressLabel,
                    _checkButton,
                    _downloadButton,
                    _viewReleaseButton
                }
            }
        };

        // Store references to title labels so we can toggle visibility
        latestVersionTitleLabel.BindingContext = this;
        releaseNotesTitleLabel.BindingContext = this;

        Content = scrollView;

        // Store title labels for later access
        _latestVersionLabel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Label.IsVisible))
            {
                latestVersionTitleLabel.IsVisible = _latestVersionLabel.IsVisible;
            }
        };

        _releaseNotesEditor.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Editor.IsVisible))
            {
                releaseNotesTitleLabel.IsVisible = _releaseNotesEditor.IsVisible;
            }
        };
    }

    private async void OnCheckForUpdatesClicked(object? sender, EventArgs e)
    {
        _checkButton.IsEnabled = false;
        _statusLabel.Text = "Checking for updates...";
        _statusLabel.TextColor = Color.FromRgb(200, 200, 200);

        try
        {
            var updateInfo = await _updateManager.CheckForUpdateAsync();

            if (updateInfo == null)
            {
                _statusLabel.Text = "Unable to check for updates. Please check your internet connection.";
                _statusLabel.TextColor = Color.FromRgb(255, 150, 0);
                HideUpdateInfo();
            }
            else if (updateInfo.IsUpdateAvailable)
            {
                _statusLabel.Text = "🎉 A new update is available!";
                _statusLabel.TextColor = Color.FromRgb(0, 255, 0);
                _currentUpdateInfo = updateInfo;
                ShowUpdateInfo(updateInfo);
            }
            else
            {
                _statusLabel.Text = $"✓ You're up to date! (Version {updateInfo.CurrentVersion})";
                _statusLabel.TextColor = Color.FromRgb(0, 255, 0);
                HideUpdateInfo();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error checking for updates: {ex.Message}";
            _statusLabel.TextColor = Color.FromRgb(255, 100, 100);
            HideUpdateInfo();
        }
        finally
        {
            _checkButton.IsEnabled = true;
        }
    }

    private void ShowUpdateInfo(UpdateInfo updateInfo)
    {
        _latestVersionLabel.Text = updateInfo.LatestVersion;
        _latestVersionLabel.IsVisible = true;

        if (updateInfo.PublishedAt.HasValue)
        {
            _releaseDateLabel.Text = $"Released: {updateInfo.PublishedAt.Value:yyyy-MM-dd HH:mm}";
            _releaseDateLabel.IsVisible = true;
        }

        _fileSizeLabel.Text = $"Size: {updateInfo.FormattedFileSize}";
        _fileSizeLabel.IsVisible = true;

        if (!string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes))
        {
            _releaseNotesEditor.Text = updateInfo.ReleaseNotes;
            _releaseNotesEditor.IsVisible = true;
        }

        _downloadButton.IsVisible = true;
        _viewReleaseButton.IsVisible = true;
    }

    private void HideUpdateInfo()
    {
        _latestVersionLabel.IsVisible = false;
        _releaseDateLabel.IsVisible = false;
        _fileSizeLabel.IsVisible = false;
        _releaseNotesEditor.IsVisible = false;
        _downloadButton.IsVisible = false;
        _viewReleaseButton.IsVisible = false;
        _progressBar.IsVisible = false;
        _progressLabel.IsVisible = false;
        _currentUpdateInfo = null;
    }

    private async void OnDownloadUpdateClicked(object? sender, EventArgs e)
    {
        if (_currentUpdateInfo == null)
            return;

        var confirm = await DisplayAlert(
            "Download Update",
            $"Download and install version {_currentUpdateInfo.LatestVersion}?\n\nSize: {_currentUpdateInfo.FormattedFileSize}",
            "Download",
            "Cancel");

        if (!confirm)
            return;

        _downloadButton.IsEnabled = false;
        _checkButton.IsEnabled = false;
        _progressBar.Progress = 0;
        _progressBar.IsVisible = true;
        _progressLabel.IsVisible = true;

        try
        {
            var progress = new Progress<double>(value =>
            {
                _progressBar.Progress = value;
                _progressLabel.Text = $"Downloading... {value:P0}";
            });

            var apkPath = await _updateManager.DownloadUpdateAsync(
                _currentUpdateInfo.DownloadUrl,
                progress);

            _progressLabel.Text = "Download complete! Installing...";

            await Task.Delay(500); // Brief pause to show completion

            // Install the APK
            _updateManager.InstallUpdate(apkPath);

            // Note: After starting installation, the app may be closed by the system
            await DisplayAlert(
                "Install Update",
                "Please follow the on-screen instructions to install the update.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Download Failed",
                $"Failed to download update: {ex.Message}",
                "OK");

            _progressBar.IsVisible = false;
            _progressLabel.IsVisible = false;
        }
        finally
        {
            _downloadButton.IsEnabled = true;
            _checkButton.IsEnabled = true;
        }
    }

    private async void OnViewReleaseClicked(object? sender, EventArgs e)
    {
        if (_currentUpdateInfo == null || string.IsNullOrWhiteSpace(_currentUpdateInfo.ReleaseUrl))
            return;

        try
        {
            await Browser.Default.OpenAsync(_currentUpdateInfo.ReleaseUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open browser: {ex.Message}", "OK");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Clean up old downloaded APKs
        _updateManager.CleanupOldUpdates();
    }
}
