namespace Ouroboros.ApiHost.Services;

/// <summary>
/// Service for self-model operations.
/// </summary>
public interface ISelfModelService
{
    /// <summary>
    /// Gets the current agent state.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Agent state response</returns>
    Task<SelfStateResponse> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets forecast information.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Forecast response</returns>
    Task<SelfForecastResponse> GetForecastsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets active commitments.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of commitments</returns>
    Task<List<CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates a self-explanation from execution DAG.
    /// </summary>
    /// <param name="request">Explanation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Explanation response</returns>
    Task<SelfExplainResponse> ExplainAsync(SelfExplainRequest request, CancellationToken ct = default);
}