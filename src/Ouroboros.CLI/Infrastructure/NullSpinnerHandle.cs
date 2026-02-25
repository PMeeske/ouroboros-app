namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// No-op spinner for quiet mode.
/// </summary>
internal sealed class NullSpinnerHandle : ISpinnerHandle
{
    public static readonly NullSpinnerHandle Instance = new();
    public void UpdateLabel(string label) { }
    public void Dispose() { }
}