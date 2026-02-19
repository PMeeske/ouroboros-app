using System.Text;
using Ouroboros.Android.Services;
using Ouroboros.Android.Views;

namespace Ouroboros.Android;

/// <summary>
/// Main page with CLI-like interface backed by the Ouroboros WebAPI.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly CliExecutor? _cliExecutor;
    private readonly StringBuilder _outputHistory;
    private readonly List<string> _commandHistory;
    private int _historyIndex;
    private CommandSuggestionEngine? _suggestionEngine;

    public MainPage()
    {
        InitializeComponent();

        _outputHistory = new StringBuilder();
        _commandHistory = new List<string>();
        _historyIndex = -1;

        _outputHistory.AppendLine("Ouroboros CLI v2.0");
        _outputHistory.AppendLine();
        _outputHistory.AppendLine("Welcome! Here are some things you can do:");
        _outputHistory.AppendLine("  ask <question>   Ask the AI a question");
        _outputHistory.AppendLine("  pipeline <dsl>   Run a DSL pipeline");
        _outputHistory.AppendLine("  status           Check connection to WebAPI");
        _outputHistory.AppendLine("  help             Show all commands");
        _outputHistory.AppendLine();
        _outputHistory.AppendLine("Tip: Tap the quick-action buttons below or type a command.");
        _outputHistory.AppendLine();

        // Initialize with database support
        try
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "command_history.db");
            var apiEndpoint = Preferences.Get("api_endpoint", "http://localhost:5000");
            _cliExecutor = new CliExecutor(dbPath, apiEndpoint);

            try
            {
                var historyService = new CommandHistoryService(dbPath);
                _suggestionEngine = new CommandSuggestionEngine(historyService);
            }
            catch (Exception ex)
            {
                _suggestionEngine = null;
                _outputHistory.AppendLine($"? Suggestions unavailable: {ex.Message}");
                _outputHistory.AppendLine();
            }
        }
        catch (Exception ex)
        {
            _outputHistory.AppendLine($"? Initialization error: {ex.Message}");
            _outputHistory.AppendLine("Some features may be unavailable.");
            _outputHistory.AppendLine();

            try
            {
                _cliExecutor = new CliExecutor(null);
            }
            catch
            {
                _cliExecutor = null!;
            }
        }

        _outputHistory.Append("> ");
        UpdateOutput();
    }

    private async void OnCommandEntered(object? sender, EventArgs e)
    {
        await ExecuteCommand();
    }

    private async void OnExecuteClicked(object? sender, EventArgs e)
    {
        await ExecuteCommand();
    }

    private async void OnCommandTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suggestionEngine == null || !Preferences.Get("auto_suggest", true))
        {
            SuggestionsFrame.IsVisible = false;
            return;
        }

        var text = e.NewTextValue?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            SuggestionsFrame.IsVisible = false;
            return;
        }

        try
        {
            var suggestions = await _suggestionEngine.GetSuggestionsAsync(text, 5);

            if (suggestions.Count > 0)
            {
                SuggestionsView.ItemsSource = suggestions;
                SuggestionsFrame.IsVisible = true;
            }
            else
            {
                SuggestionsFrame.IsVisible = false;
            }
        }
        catch
        {
            SuggestionsFrame.IsVisible = false;
        }
    }

    private void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CommandSuggestion suggestion)
        {
            CommandEntry.Text = suggestion.Command;
            SuggestionsFrame.IsVisible = false;
            SuggestionsView.SelectedItem = null;
        }
    }

    private void OnHistoryUpClicked(object? sender, EventArgs e)
    {
        if (_commandHistory.Count == 0)
            return;

        if (_historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            CommandEntry.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
        }
    }

    private async void OnQuickCommand(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            CommandEntry.Text = button.Text;
            await ExecuteCommand();
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsView = new SettingsView();
        settingsView.SettingsChanged += (s, args) =>
        {
            if (_cliExecutor != null)
            {
                _cliExecutor.ApiEndpoint = args.ApiEndpoint;
                _outputHistory.AppendLine($"Settings updated: Endpoint = {args.ApiEndpoint}");
                UpdateOutput();
            }
        };
        await Navigation.PushAsync(settingsView);
    }

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet(
            "Menu",
            "Cancel",
            null,
            "Help",
            "About",
            "Status",
            "Clear Screen",
            "Check for Updates",
            "Settings");

        switch (action)
        {
            case "Help":
                CommandEntry.Text = "help";
                await ExecuteCommand();
                break;
            case "About":
                CommandEntry.Text = "about";
                await ExecuteCommand();
                break;
            case "Status":
                CommandEntry.Text = "status";
                await ExecuteCommand();
                break;
            case "Clear Screen":
                CommandEntry.Text = "clear";
                await ExecuteCommand();
                break;
            case "Check for Updates":
                await OnCheckForUpdatesClicked();
                break;
            case "Settings":
                OnSettingsClicked(sender, e);
                break;
        }
    }

    private async Task OnCheckForUpdatesClicked()
    {
        try
        {
            var updateManager = Application.Current?.Handler?.MauiContext?.Services.GetService<UpdateManagerService>();
            
            if (updateManager == null)
            {
                await DisplayAlert("Error", "Update service not available", "OK");
                return;
            }

            var updateView = new Views.UpdateView(updateManager);
            await Navigation.PushAsync(updateView);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open update view: {ex.Message}", "OK");
        }
    }

    private async Task ExecuteCommand()
    {
        var command = CommandEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(command))
            return;

        _outputHistory.AppendLine(command);
        _outputHistory.AppendLine();

        string result;
        if (_cliExecutor == null)
        {
            result = "Error: CLI executor not initialized. App may be in degraded state.";
        }
        else
        {
            try
            {
                result = await _cliExecutor.ExecuteCommandAsync(command);
            }
            catch (Exception ex)
            {
                result = $"Error executing command: {ex.Message}";
            }
        }

        if (result == "CLEAR_SCREEN")
        {
            _outputHistory.Clear();
            _outputHistory.AppendLine("Ouroboros CLI");
            _outputHistory.AppendLine();
        }
        else
        {
            _outputHistory.AppendLine(result);
            _outputHistory.AppendLine();
        }

        _outputHistory.Append("> ");

        UpdateOutput();
        CommandEntry.Text = string.Empty;

        await Task.Delay(100);
        await OutputScrollView.ScrollToAsync(OutputLabel, ScrollToPosition.End, true);
    }

    private void UpdateOutput()
    {
        OutputLabel.Text = _outputHistory.ToString();
    }
}
