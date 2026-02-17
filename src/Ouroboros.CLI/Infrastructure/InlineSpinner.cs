namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Single-line spinner using carriage-return overwrite.
/// </summary>
internal sealed class InlineSpinner : ISpinnerHandle
{
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private readonly Timer _timer;
    private readonly object _consoleLock;
    private string _label;
    private int _frame;
    private bool _disposed;

    public InlineSpinner(string label, object consoleLock)
    {
        _label = label;
        _consoleLock = consoleLock;
        _timer = new Timer(_ => Render(), null, 0, 80);
    }

    private void Render()
    {
        if (_disposed) return;
        var frame = Frames[_frame++ % Frames.Length];

        lock (_consoleLock)
        {
            if (_disposed) return;
            try
            {
                var width = Math.Max(Console.WindowWidth, 40);
                var text = $"\r  {frame} {_label}";
                Console.Write(text.PadRight(width - 1));
            }
            catch
            {
                // Console may not be available in redirected scenarios
            }
        }
    }

    public void UpdateLabel(string label) => _label = label;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();

        lock (_consoleLock)
        {
            try
            {
                var width = Math.Max(Console.WindowWidth, 40);
                Console.Write($"\r{"".PadRight(width - 1)}\r");
            }
            catch
            {
                // Console may not be available in redirected scenarios
            }
        }
    }
}