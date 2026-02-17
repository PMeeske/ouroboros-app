using Ouroboros.Abstractions.Core;

internal sealed class MockChatModel : IChatCompletionModel
{
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult("Mock response");
    }
}