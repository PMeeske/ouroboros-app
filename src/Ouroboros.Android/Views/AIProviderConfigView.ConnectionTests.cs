using Ouroboros.Android.Services;

namespace Ouroboros.Android.Views;

/// <summary>
/// Provider-specific connection test methods.
/// </summary>
public partial class AIProviderConfigView
{
    private async Task<TestConnectionResult> TestProviderConnectionAsync(AIProviderConfig config)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        try
        {
            switch (config.Provider)
            {
                case AIProvider.Ollama:
                    return await TestOllamaAsync(httpClient, config);

                case AIProvider.OpenAI:
                    return await TestOpenAIAsync(httpClient, config);

                case AIProvider.Anthropic:
                    return await TestAnthropicAsync(httpClient, config);

                case AIProvider.Google:
                    return await TestGoogleAsync(httpClient, config);

                case AIProvider.Meta:
                case AIProvider.Mistral:
                    return await TestOpenAICompatibleAsync(httpClient, config);

                case AIProvider.Cohere:
                    return await TestCohereAsync(httpClient, config);

                case AIProvider.HuggingFace:
                    return await TestHuggingFaceAsync(httpClient, config);

                case AIProvider.AzureOpenAI:
                    return await TestAzureOpenAIAsync(httpClient, config);

                default:
                    return new TestConnectionResult
                    {
                        Success = false,
                        Message = $"Provider {config.Provider} testing not implemented"
                    };
            }
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    private async Task<TestConnectionResult> TestOllamaAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            var response = await client.GetAsync($"{config.Endpoint}/api/tags");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new TestConnectionResult
                {
                    Success = true,
                    Message = $"Successfully connected to Ollama at {config.Endpoint}"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed with status code: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Cannot reach endpoint: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }

    private async Task<TestConnectionResult> TestOpenAIAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            var response = await client.GetAsync($"{config.Endpoint}/models");

            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    Success = true,
                    Message = "Successfully authenticated with OpenAI"
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Authentication failed - check API key"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }

    private async Task<TestConnectionResult> TestAnthropicAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            // Anthropic doesn't have a simple health check endpoint, so we verify the key format
            if (string.IsNullOrEmpty(config.ApiKey) || !config.ApiKey.StartsWith("sk-ant-"))
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Invalid API key format - should start with 'sk-ant-'"
                };
            }

            return new TestConnectionResult
            {
                Success = true,
                Message = "API key format is valid. Configuration saved."
            };
        }
        catch (InvalidOperationException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<TestConnectionResult> TestGoogleAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            // Test with a simple models list request
            var url = $"{config.Endpoint}/models?key={config.ApiKey}";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    Success = true,
                    Message = "Successfully authenticated with Google AI"
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Authentication failed - check API key and project ID"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }

    private async Task<TestConnectionResult> TestOpenAICompatibleAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            var response = await client.GetAsync($"{config.Endpoint}/models");

            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    Success = true,
                    Message = $"Successfully connected to {config.Provider}"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }

    private async Task<TestConnectionResult> TestCohereAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            // Cohere has a check-api-key endpoint
            var response = await client.GetAsync($"{config.Endpoint}/check-api-key");

            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    Success = true,
                    Message = "Successfully authenticated with Cohere"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Authentication failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }

    private async Task<TestConnectionResult> TestHuggingFaceAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            // Verify API key format and endpoint
            if (string.IsNullOrEmpty(config.ApiKey) || !config.ApiKey.StartsWith("hf_"))
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Invalid API key format - should start with 'hf_'"
                };
            }

            return new TestConnectionResult
            {
                Success = true,
                Message = "API key format is valid. Configuration saved."
            };
        }
        catch (InvalidOperationException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private async Task<TestConnectionResult> TestAzureOpenAIAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);

            var url = $"{config.Endpoint}/openai/deployments?api-version=2023-05-15";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult
                {
                    Success = true,
                    Message = "Successfully connected to Azure OpenAI"
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Authentication failed - check API key"
                };
            }
            else
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult
            {
                Success = false,
                Message = "Connection timeout - check endpoint and network"
            };
        }
    }
}

/// <summary>
/// Result of connection test.
/// </summary>
internal class TestConnectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
