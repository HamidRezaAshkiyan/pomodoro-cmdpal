using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Core;
using Windows.Storage.Streams;

namespace PomodoroCmdPal.Dock;

/// <summary>
/// Caches quantized 36-step PNG progress rings and creates Command Palette IconInfo streams.
/// </summary>
internal sealed class DockIconRenderer
{
    private const int TotalSteps = 36;
    private readonly ConcurrentDictionary<(PomodoroPhase Phase, PomodoroState State, int Step), byte[]> _pngCache = new();

    public (PomodoroPhase Phase, PomodoroState State, int Step) GetVisualKey(PomodoroSnapshot snapshot)
    {
        var step = (int)Math.Round(snapshot.Progress * TotalSteps);
        step = Math.Clamp(step, 0, TotalSteps);
        return (snapshot.Phase, snapshot.State, step);
    }

    public IconInfo CreateIcon(PomodoroSnapshot snapshot)
    {
        var key = GetVisualKey(snapshot);
        try
        {
            var pngBytes = _pngCache.GetOrAdd(key, k =>
                ProgressRingPngRenderer.Render(k.Phase, k.State, (double)k.Step / TotalSteps));

            var stream = new InMemoryRandomAccessStream();
            stream.WriteAsync(pngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            stream.Seek(0);
            return IconInfo.FromStream(stream);
        }
        catch
        {
            return GetFallbackGlyph(snapshot);
        }
    }

    private static IconInfo GetFallbackGlyph(PomodoroSnapshot snapshot) => snapshot.State switch
    {
        PomodoroState.Running => snapshot.Phase == PomodoroPhase.Focus
            ? new IconInfo("\uE769")
            : new IconInfo("\uEC32"),
        PomodoroState.Paused => new IconInfo("\uE768"),
        _ => new IconInfo("\uE916")
    };
}
