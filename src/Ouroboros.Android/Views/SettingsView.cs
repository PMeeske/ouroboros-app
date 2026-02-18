using Microsoft.Maui.Controls;
using Ouroboros.Android.Services;

namespace Ouroboros.Android.Views;

/// <summary>
/// Settings page — configure the Ouroboros WebAPI endpoint and local preferences.
/// </summary>
public class SettingsView : ContentPage
{
    private readonly Entry _endpointEntry;
    private readonly Switch _autoSuggestSwitch;
    private readonly Switch _commandHistorySwitch;
    private readonly Slider _historyLimitSlider;
    private readonly Label _historyLimitLabel;

    /// <summary>
    /// Event fired when settings are saved.
    /// </summary>
    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public SettingsView()
    {
        Title = "Settings";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        // ── WebAPI Endpoint ───────────────────────────────────────────
        var endpointLabel = new Label
        {
            Text = "Ouroboros WebAPI Endpoint",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        var endpointHint = new Label
        {
            Text = "URL of the Ouroboros WebAPI server (e.g. http://192.168.1.100:5000)",
            TextColor = Color.FromRgb(128, 128, 128),
            FontSize = 12,
            Margin = new Thickness(10, 0, 10, 5)
        };

        _endpointEntry = new Entry
        {
            Placeholder = "http://localhost:5000",
            PlaceholderColor = Color.FromRgb(128, 128, 128),
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            Margin = new Thickness(10, 0, 10, 10)
        };

        // ── Test Connection Button ────────────────────────────────────
        var testButton = new Button
        {
            Text = "Test Connection",
            BackgroundColor = Color.FromRgb(0, 100, 170),
            TextColor = Colors.White,
            Margin = new Thickness(10, 0, 10, 10)
        };
        testButton.Clicked += OnTestConnectionClicked;

        // ── Auto-suggest ──────────────────────────────────────────────
        var autoSuggestLabel = new Label
        {
            Text = "Enable Auto-Suggestions",
            TextColor = Color.FromRgb(0, 255, 0),
            Margin = new Thickness(10, 20, 10, 5)
        };

        _autoSuggestSwitch = new Switch
        {
            OnColor = Color.FromRgb(0, 170, 0),
            IsToggled = true,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // ── Command History ───────────────────────────────────────────
        var historyLabel = new Label
        {
            Text = "Enable Command History",
            TextColor = Color.FromRgb(0, 255, 0),
            Margin = new Thickness(10, 20, 10, 5)
        };

        _commandHistorySwitch = new Switch
        {
            OnColor = Color.FromRgb(0, 170, 0),
            IsToggled = true,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // ── History Limit ─────────────────────────────────────────────
        var historyLimitTitleLabel = new Label
        {
            Text = "History Limit",
            TextColor = Color.FromRgb(0, 255, 0),
            Margin = new Thickness(10, 20, 10, 5)
        };

        _historyLimitLabel = new Label
        {
            Text = "1000 commands",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(10, 0, 10, 5)
        };

        _historyLimitSlider = new Slider
        {
            Minimum = 100,
            Maximum = 5000,
            Value = 1000,
            MinimumTrackColor = Color.FromRgb(0, 170, 0),
            MaximumTrackColor = Color.FromRgb(100, 100, 100),
            Margin = new Thickness(10, 0, 10, 10)
        };

        _historyLimitSlider.ValueChanged += (s, e) =>
        {
            _historyLimitLabel.Text = $"{(int)e.NewValue} commands";
        };

        // ── Buttons ───────────────────────────────────────────────────
        var saveButton = new Button
        {
            Text = "Save Settings",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10, 30, 10, 10)
        };
        saveButton.Clicked += OnSaveClicked;

        var reasoningButton = new Button
        {
            Text = "Symbolic Reasoning",
            BackgroundColor = Color.FromRgb(100, 0, 170),
            TextColor = Colors.White,
            Margin = new Thickness(10, 10, 10, 10)
        };
        reasoningButton.Clicked += OnSymbolicReasoningClicked;

        var clearHistoryButton = new Button
        {
            Text = "Clear Command History",
            BackgroundColor = Color.FromRgb(170, 0, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10, 10, 10, 10)
        };
        clearHistoryButton.Clicked += OnClearHistoryClicked;

        Content = new ScrollView
        {
            Content = new StackLayout
            {
                Children =
                {
                    endpointLabel,
                    endpointHint,
                    _endpointEntry,
                    testButton,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 20) },
                    autoSuggestLabel,
                    _autoSuggestSwitch,
                    historyLabel,
                    _commandHistorySwitch,
                    historyLimitTitleLabel,
                    _historyLimitLabel,
                    _historyLimitSlider,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 20) },
                    saveButton,
                    reasoningButton,
                    clearHistoryButton
                }
            }
        };

        LoadSettings();
    }

    private void LoadSettings()
    {
        _endpointEntry.Text = Preferences.Get("api_endpoint", "http://localhost:5000");
        _autoSuggestSwitch.IsToggled = Preferences.Get("auto_suggest", true);
        _commandHistorySwitch.IsToggled = Preferences.Get("command_history", true);
        _historyLimitSlider.Value = Preferences.Get("history_limit", 1000);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var endpoint = _endpointEntry.Text?.Trim() ?? "http://localhost:5000";

        if (!string.IsNullOrEmpty(endpoint) && !endpoint.StartsWith("http"))
        {
            await DisplayAlert("Invalid Endpoint", "Endpoint must start with http:// or https://", "OK");
            return;
        }

        Preferences.Set("api_endpoint", endpoint);
        Preferences.Set("auto_suggest", _autoSuggestSwitch.IsToggled);
        Preferences.Set("command_history", _commandHistorySwitch.IsToggled);
        Preferences.Set("history_limit", (int)_historyLimitSlider.Value);

        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
        {
            ApiEndpoint = endpoint,
            AutoSuggest = _autoSuggestSwitch.IsToggled,
            CommandHistory = _commandHistorySwitch.IsToggled,
            HistoryLimit = (int)_historyLimitSlider.Value
        });

        await DisplayAlert("Settings Saved", "Your settings have been saved successfully.", "OK");
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        var endpoint = _endpointEntry.Text?.Trim() ?? "http://localhost:5000";

        using var client = new OuroborosApiClient(endpoint);
        var healthy = await client.IsHealthyAsync();

        await DisplayAlert(
            healthy ? "Connected" : "Connection Failed",
            healthy
                ? $"Successfully connected to {endpoint}"
                : $"Could not reach {endpoint}.\n\nEnsure the Ouroboros WebAPI is running.",
            "OK");
    }

    private async void OnClearHistoryClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Clear History",
            "Are you sure you want to clear all command history?",
            "Yes",
            "No");

        if (confirm)
        {
            try
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "command_history.db");
                var historyService = new CommandHistoryService(dbPath);
                await historyService.ClearHistoryAsync();
                await DisplayAlert("Success", "Command history cleared successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to clear history: {ex.Message}", "OK");
            }
        }
    }

    private async void OnSymbolicReasoningClicked(object? sender, EventArgs e)
    {
        var reasoningView = new SymbolicReasoningView();
        await Navigation.PushAsync(reasoningView);
    }
}

/// <summary>
/// Event args for settings changed event.
/// </summary>
public class SettingsChangedEventArgs : EventArgs
{
    /// <summary>Ouroboros WebAPI endpoint URL.</summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>Whether auto-suggest is enabled.</summary>
    public bool AutoSuggest { get; set; }

    /// <summary>Whether command history is enabled.</summary>
    public bool CommandHistory { get; set; }

    /// <summary>Maximum number of history entries to keep.</summary>
    public int HistoryLimit { get; set; }
}
