namespace Ouroboros.Application.Integration;

/// <summary>Options for program synthesis configuration.</summary>
public sealed record ProgramSynthesisOptions(
    string TargetLanguage = "CSharp",
    int MaxSynthesisAttempts = 5,
    bool EnableVerification = true);