using Microsoft.Maui.Controls;
using Ouroboros.Android.Services;

namespace Ouroboros.Android.Views;

/// <summary>
/// View for configuring AI providers.
/// </summary>
public partial class AIProviderConfigView : ContentPage
{
    private readonly AIProviderService _providerService;
    private readonly Picker _providerPicker;
    private readonly Entry _endpointEntry;
    private readonly Entry _apiKeyEntry;
    private readonly Entry _modelEntry;
    private readonly Entry _organizationEntry;
    private readonly Entry _projectEntry;
    private readonly Entry _regionEntry;
    private readonly Entry _deploymentEntry;
    private readonly Slider _temperatureSlider;
    private readonly Label _temperatureLabel;
    private readonly Slider _maxTokensSlider;
    private readonly Label _maxTokensLabel;
    private readonly Switch _enabledSwitch;
    private readonly Label _statusLabel;
    private readonly Label _providerHintLabel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConfigView"/> class.
    /// </summary>
    public AIProviderConfigView()
    {
        _providerService = new AIProviderService();

        Title = "AI Provider Configuration";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        // Provider Selection
        var providerLabel = new Label
        {
            Text = "AI Provider",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        _providerPicker = new Picker
        {
            Title = "Select Provider",
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            Margin = new Thickness(10, 0, 10, 10)
        };

        foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
        {
            var config = AIProviderConfig.GetDefault(provider);
            _providerPicker.Items.Add(config.GetDisplayName());
        }

        _providerPicker.SelectedIndexChanged += OnProviderChanged;

        _statusLabel = new Label
        {
            TextColor = Color.FromRgb(200, 200, 200),
            FontSize = 12,
            Margin = new Thickness(10, 0, 10, 5)
        };

        _providerHintLabel = new Label
        {
            TextColor = Color.FromRgb(160, 160, 160),
            FontSize = 11,
            Margin = new Thickness(10, 0, 10, 10),
            LineBreakMode = LineBreakMode.WordWrap
        };

        // Endpoint
        var endpointLabel = CreateLabel("Endpoint URL");
        _endpointEntry = CreateEntry("https://api.example.com");

        // API Key
        var apiKeyLabel = CreateLabel("API Key");
        _apiKeyEntry = CreateEntry("Enter API key here", isPassword: true);

        // Model
        var modelLabel = CreateLabel("Default Model");
        _modelEntry = CreateEntry("e.g. llama3, gpt-4o, claude-sonnet-4-20250514");

        // Organization ID (for OpenAI)
        var orgLabel = CreateLabel("Organization ID (Optional)");
        _organizationEntry = CreateEntry("org-id");

        // Project ID (for Google)
        var projectLabel = CreateLabel("Project ID (Optional)");
        _projectEntry = CreateEntry("project-id");

        // Region (for Azure)
        var regionLabel = CreateLabel("Region (Optional)");
        _regionEntry = CreateEntry("eastus");

        // Deployment (for Azure OpenAI)
        var deploymentLabel = CreateLabel("Deployment Name (Optional)");
        _deploymentEntry = CreateEntry("deployment-name");

        // Temperature -- controls randomness of the output
        var temperatureTitle = CreateLabel("Temperature (0 = precise, 2 = creative)");
        _temperatureLabel = new Label
        {
            Text = "0.7",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(10, 0, 10, 5)
        };

        _temperatureSlider = new Slider
        {
            Minimum = 0,
            Maximum = 2,
            Value = 0.7,
            MinimumTrackColor = Color.FromRgb(0, 170, 0),
            MaximumTrackColor = Color.FromRgb(100, 100, 100),
            Margin = new Thickness(10, 0, 10, 10)
        };

        _temperatureSlider.ValueChanged += (s, e) =>
        {
            _temperatureLabel.Text = $"{e.NewValue:F2}";
        };

        // Max Tokens -- controls maximum response length
        var maxTokensTitle = CreateLabel("Max Tokens (response length limit)");
        _maxTokensLabel = new Label
        {
            Text = "2000",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(10, 0, 10, 5)
        };

        _maxTokensSlider = new Slider
        {
            Minimum = 100,
            Maximum = 8000,
            Value = 2000,
            MinimumTrackColor = Color.FromRgb(0, 170, 0),
            MaximumTrackColor = Color.FromRgb(100, 100, 100),
            Margin = new Thickness(10, 0, 10, 10)
        };

        _maxTokensSlider.ValueChanged += (s, e) =>
        {
            _maxTokensLabel.Text = $"{(int)e.NewValue}";
        };

        // Enabled Switch
        var enabledLabel = CreateLabel("Enable this provider");
        _enabledSwitch = new Switch
        {
            OnColor = Color.FromRgb(0, 170, 0),
            IsToggled = true,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // Buttons
        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(10, 30, 10, 10)
        };

        var saveButton = new Button
        {
            Text = "Save Configuration",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        saveButton.Clicked += OnSaveClicked;

        var testButton = new Button
        {
            Text = "Test Connection",
            BackgroundColor = Color.FromRgb(0, 100, 170),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        testButton.Clicked += OnTestClicked;

        buttonStack.Children.Add(saveButton);
        buttonStack.Children.Add(testButton);

        var setActiveButton = new Button
        {
            Text = "Set as Active Provider",
            BackgroundColor = Color.FromRgb(170, 100, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10, 0, 10, 10)
        };
        setActiveButton.Clicked += OnSetActiveClicked;

        Content = new ScrollView
        {
            Content = new StackLayout
            {
                Children =
                {
                    providerLabel,
                    _providerPicker,
                    _statusLabel,
                    _providerHintLabel,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    endpointLabel,
                    _endpointEntry,
                    apiKeyLabel,
                    _apiKeyEntry,
                    modelLabel,
                    _modelEntry,
                    orgLabel,
                    _organizationEntry,
                    projectLabel,
                    _projectEntry,
                    regionLabel,
                    _regionEntry,
                    deploymentLabel,
                    _deploymentEntry,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    temperatureTitle,
                    _temperatureLabel,
                    _temperatureSlider,
                    maxTokensTitle,
                    _maxTokensLabel,
                    _maxTokensSlider,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    enabledLabel,
                    _enabledSwitch,
                    buttonStack,
                    setActiveButton
                }
            }
        };

        // Load active provider
        LoadActiveProvider();
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextColor = Color.FromRgb(0, 255, 0),
            Margin = new Thickness(10, 10, 10, 5)
        };
    }

    private Entry CreateEntry(string placeholder, bool isPassword = false)
    {
        return new Entry
        {
            Placeholder = placeholder,
            PlaceholderColor = Color.FromRgb(128, 128, 128),
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            IsPassword = isPassword,
            Margin = new Thickness(10, 0, 10, 10)
        };
    }

    private void LoadActiveProvider()
    {
        var activeProvider = _providerService.GetActiveProvider();
        _providerPicker.SelectedIndex = (int)activeProvider;
        LoadProviderConfig(activeProvider);
    }

    private void LoadProviderConfig(AIProvider provider)
    {
        var config = _providerService.GetProviderConfig(provider)
                     ?? AIProviderConfig.GetDefault(provider);

        _endpointEntry.Text = config.Endpoint;
        _apiKeyEntry.Text = config.ApiKey ?? string.Empty;
        _modelEntry.Text = config.DefaultModel ?? string.Empty;
        _organizationEntry.Text = config.OrganizationId ?? string.Empty;
        _projectEntry.Text = config.ProjectId ?? string.Empty;
        _regionEntry.Text = config.Region ?? string.Empty;
        _deploymentEntry.Text = config.DeploymentName ?? string.Empty;
        _temperatureSlider.Value = config.Temperature ?? 0.7;
        _maxTokensSlider.Value = config.MaxTokens ?? 2000;
        _enabledSwitch.IsToggled = config.IsEnabled;

        var isActive = _providerService.GetActiveProvider() == provider;
        _statusLabel.Text = isActive ? "✓ Active Provider" : "Inactive";
        _statusLabel.TextColor = isActive ? Color.FromRgb(0, 255, 0) : Color.FromRgb(200, 200, 200);

        _providerHintLabel.Text = GetProviderHint(provider);
    }

    private static string GetProviderHint(AIProvider provider) => provider switch
    {
        AIProvider.Ollama =>
            "Runs models locally on your device or a nearby server. No API key needed. " +
            "Install Ollama from ollama.com, then pull a model (e.g. \"ollama pull llama3\").",
        AIProvider.OpenAI =>
            "Cloud provider. Requires an API key from platform.openai.com. " +
            "Popular models: gpt-4o, gpt-4o-mini.",
        AIProvider.Anthropic =>
            "Cloud provider. Requires an API key starting with \"sk-ant-\" from console.anthropic.com. " +
            "Popular models: claude-sonnet-4-20250514, claude-haiku-4-5-20251001.",
        AIProvider.Google =>
            "Cloud provider. Requires a Google AI API key from aistudio.google.com. " +
            "Popular models: gemini-2.0-flash, gemini-1.5-pro.",
        AIProvider.AzureOpenAI =>
            "Enterprise cloud. Requires an Azure resource endpoint, API key, and deployment name. " +
            "Contact your Azure admin for details.",
        AIProvider.HuggingFace =>
            "Open-source model hub. Requires an API key starting with \"hf_\" from huggingface.co/settings/tokens.",
        AIProvider.Cohere =>
            "Cloud provider focused on enterprise search and generation. " +
            "Get an API key from dashboard.cohere.com.",
        _ =>
            "Select a provider above to see setup instructions."
    };

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex >= 0)
        {
            var provider = (AIProvider)_providerPicker.SelectedIndex;
            LoadProviderConfig(provider);
        }
    }
}
