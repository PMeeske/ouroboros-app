using Ouroboros.Android.Services;

namespace Ouroboros.Android.Views;

/// <summary>
/// Event handlers for save, test, and set-active actions.
/// </summary>
public partial class AIProviderConfigView
{
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;

        var config = new AIProviderConfig
        {
            Provider = provider,
            Endpoint = _endpointEntry.Text?.Trim() ?? string.Empty,
            ApiKey = _apiKeyEntry.Text?.Trim(),
            DefaultModel = _modelEntry.Text?.Trim(),
            OrganizationId = _organizationEntry.Text?.Trim(),
            ProjectId = _projectEntry.Text?.Trim(),
            Region = _regionEntry.Text?.Trim(),
            DeploymentName = _deploymentEntry.Text?.Trim(),
            Temperature = _temperatureSlider.Value,
            MaxTokens = (int)_maxTokensSlider.Value,
            IsEnabled = _enabledSwitch.IsToggled
        };

        var (isValid, error) = config.Validate();

        if (!isValid)
        {
            await DisplayAlert("Validation Error", error, "OK");
            return;
        }

        _providerService.SaveProviderConfig(config);
        await DisplayAlert("Success", $"{config.GetDisplayName()} configuration saved", "OK");
    }

    private async void OnTestClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider first", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;
        var config = BuildConfigFromUI(provider);

        // Validate first
        var (isValid, error) = config.Validate();
        if (!isValid)
        {
            await DisplayAlert("Validation Error", error, "OK");
            return;
        }

        // Show loading
        _statusLabel.Text = "Testing connection...";
        _statusLabel.TextColor = Color.FromRgb(255, 165, 0); // Orange

        try
        {
            var testResult = await TestProviderConnectionAsync(config);

            if (testResult.Success)
            {
                _statusLabel.Text = $"✓ {testResult.Message}";
                _statusLabel.TextColor = Color.FromRgb(0, 255, 0); // Green
                await DisplayAlert("Connection Test", testResult.Message, "OK");
            }
            else
            {
                _statusLabel.Text = $"✗ {testResult.Message}";
                _statusLabel.TextColor = Color.FromRgb(255, 0, 0); // Red
                await DisplayAlert("Connection Failed", testResult.Message, "OK");
            }
        }
        catch (HttpRequestException ex)
        {
            _statusLabel.Text = $"✗ Test failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromRgb(255, 0, 0); // Red
            await DisplayAlert("Error", $"Connection test failed: {ex.Message}", "OK");
        }
        catch (TaskCanceledException ex)
        {
            _statusLabel.Text = $"✗ Test failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromRgb(255, 0, 0); // Red
            await DisplayAlert("Error", $"Connection test timed out: {ex.Message}", "OK");
        }
    }

    private AIProviderConfig BuildConfigFromUI(AIProvider provider)
    {
        return new AIProviderConfig
        {
            Provider = provider,
            Endpoint = _endpointEntry.Text?.Trim() ?? string.Empty,
            ApiKey = _apiKeyEntry.Text?.Trim(),
            DefaultModel = _modelEntry.Text?.Trim(),
            OrganizationId = _organizationEntry.Text?.Trim(),
            ProjectId = _projectEntry.Text?.Trim(),
            Region = _regionEntry.Text?.Trim(),
            DeploymentName = _deploymentEntry.Text?.Trim(),
            Temperature = _temperatureSlider.Value,
            MaxTokens = (int)_maxTokensSlider.Value,
            IsEnabled = _enabledSwitch.IsToggled
        };
    }

    private async void OnSetActiveClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;
        _providerService.SetActiveProvider(provider);

        LoadActiveProvider();
        await DisplayAlert("Success", $"{AIProviderConfig.GetDefault(provider).GetDisplayName()} is now the active provider", "OK");
    }
}
