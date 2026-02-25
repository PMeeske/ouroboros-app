namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Handle returned by <see cref="IConsoleOutput.StartSpinner"/>.
/// </summary>
public interface ISpinnerHandle : IDisposable
{
    void UpdateLabel(string label);
}