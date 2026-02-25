namespace Ouroboros.Application.Personality;

/// <summary>
/// Communication style fingerprint for person identification.
/// </summary>
public sealed record CommunicationStyle(
    double Verbosity,           // 0-1: terse to verbose
    double QuestionFrequency,   // 0-1: statements only to mostly questions
    double EmoticonUsage,       // 0-1: no emoticons to heavy usage
    double PunctuationStyle,    // 0-1: minimal to expressive (!!, ??, ...)
    double AverageMessageLength,
    string[] PreferredGreetings,
    string[] PreferredClosings)
{
    /// <summary>Default communication style.</summary>
    public static CommunicationStyle Default => new(
        Verbosity: 0.5,
        QuestionFrequency: 0.3,
        EmoticonUsage: 0.1,
        PunctuationStyle: 0.5,
        AverageMessageLength: 50,
        PreferredGreetings: Array.Empty<string>(),
        PreferredClosings: Array.Empty<string>());

    /// <summary>Calculates similarity to another style (0-1).</summary>
    public double SimilarityTo(CommunicationStyle other)
    {
        double verbDiff = Math.Abs(Verbosity - other.Verbosity);
        double questDiff = Math.Abs(QuestionFrequency - other.QuestionFrequency);
        double emoDiff = Math.Abs(EmoticonUsage - other.EmoticonUsage);
        double punctDiff = Math.Abs(PunctuationStyle - other.PunctuationStyle);
        double lenDiff = Math.Min(1.0, Math.Abs(AverageMessageLength - other.AverageMessageLength) / 200.0);

        // Average difference, inverted to similarity
        return 1.0 - (verbDiff + questDiff + emoDiff + punctDiff + lenDiff) / 5.0;
    }
}