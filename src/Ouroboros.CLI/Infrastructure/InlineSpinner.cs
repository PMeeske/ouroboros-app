namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Single-line spinner using carriage-return overwrite.
///
/// Upgraded to match Crush's animation model:
///   • 20 FPS (50 ms tick) — smooth braille rotation
///   • Independent ellipsis sub-animation (400 ms cycle: · .. ...)
///     appended after the label so the line feels "alive" even when the
///     spinner frame hasn't changed noticeably
/// </summary>
internal sealed class InlineSpinner : ISpinnerHandle
{
    // Braille spinner frames — same as Crush
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    // Ellipsis cycles at 400 ms / 50 ms = every 8 ticks
    private static readonly string[] Ellipsis = [".", "..", "..."];
    private const int EllipsisPeriod = 8;   // ticks per ellipsis step

    private readonly Timer _timer;
    private readonly object _consoleLock;
    private string _label;
    private int _tick;
    private bool _disposed;

    public InlineSpinner(string label, object consoleLock)
    {
        _label = label;
        _consoleLock = consoleLock;
        _timer = new Timer(_ => Render(), null, 0, 50);   // 20 FPS
    }

    private void Render()
    {
        if (_disposed) return;

        var tick = _tick++;
        var frame = Frames[tick % Frames.Length];
        var dots  = Ellipsis[tick / EllipsisPeriod % Ellipsis.Length];

        lock (_consoleLock)
        {
            if (_disposed) return;
            try
            {
                var width = Math.Max(Console.WindowWidth, 40);
                Console.Write($"\r  {frame} {_label}{dots}".PadRight(width - 1));
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
