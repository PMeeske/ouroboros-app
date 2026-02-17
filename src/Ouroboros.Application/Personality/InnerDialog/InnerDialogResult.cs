namespace Ouroboros.Application.Personality;

/// <summary>
/// The result of an inner dialog process.
/// </summary>
public sealed record InnerDialogResult(
    InnerDialogSession Session,
    string SuggestedResponseTone,
    string[] KeyInsights,
    string? ProactiveQuestion,
    Dictionary<string, object> ResponseGuidance)
{
    /// <summary>Gets whether the dialog was successful.</summary>
    public bool IsSuccessful => Session.FinalDecision != null;
}