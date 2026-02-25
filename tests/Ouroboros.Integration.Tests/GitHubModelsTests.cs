namespace Ouroboros.Tests.IntegrationTests;

/// <summary>
/// xUnit wrapper for GitHub Models integration tests.
/// </summary>
[Trait("Category", "Integration")]
public class GitHubModelsTests
{
    /// <summary>
    /// Runs all GitHub Models integration tests.
    /// </summary>
    [Fact(Skip = "Integration test stub - requires external service configuration")]
    public async Task RunGitHubModelsIntegrationTests()
    {
        await Task.CompletedTask;
    }
}
