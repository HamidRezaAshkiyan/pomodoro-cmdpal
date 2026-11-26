using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Core;
using Windows.Storage.Streams;

namespace PomodoroCmdPal.Dock;

/// <summary>
/// Caches quantized PNG progress rings (36 steps normally, 60-step smooth drain + breathing pulse
/// in the final minute) and 5-frame completion celebration bursts.
/// </summary>
internal sealed class DockIconRenderer
{
    private const int NormalSteps = 36;
    private const int FinalMinuteSteps = 60;

    private readonly ConcurrentDictionary<(PomodoroPhase Phase, PomodoroState State, int Step, int MaxSteps, bool PulseHigh, bool IsFinalMinute), byte[]> _pngCache = new();
    private readonly ConcurrentDictionary<(PomodoroPhase CompletedPhase, int FrameIndex), byte[]> _celebrationCache = new();

    public (PomodoroPhase Phase, PomodoroState State, int Step, int MaxSteps, bool PulseHigh, bool IsFinalMinute) GetVisualKey(PomodoroSnapshot snapshot)
    {
        var isFinalMinute = snapshot.IsFinalMinute;
        var maxSteps = isFinalMinute ? FinalMinuteSteps : NormalSteps;
        var step = (int)Math.Round(snapshot.Progress * maxSteps);
        step = Math.Clamp(step, 0, maxSteps);
        var pulseHigh = isFinalMinute && ((int)Math.Ceiling(snapshot.Remaining.TotalSeconds) % 2 == 0);
        return (snapshot.Phase, snapshot.State, step, maxSteps, pulseHigh, isFinalMinute);
    }

    public IconInfo CreateIcon(PomodoroSnapshot snapshot)
    {
        var key = GetVisualKey(snapshot);
        try
        {
            var pngBytes = _pngCache.GetOrAdd(key, k =>
                ProgressRingPngRenderer.Render(
                    k.Phase,
                    k.State,
                    (double)k.Step / k.MaxSteps,
                    finalMinutePulseHigh: k.PulseHigh,
                    isFinalMinute: k.IsFinalMinute));

            return FromPngBytes(pngBytes);
        }
        catch
        {
            return GetFallbackGlyph(snapshot);
        }
    }

    public IconInfo CreateCelebrationIcon(PomodoroPhase completedPhase, int frameIndex, int totalFrames = 5)
    {
        try
        {
            var pngBytes = _celebrationCache.GetOrAdd((completedPhase, frameIndex), k =>
                ProgressRingPngRenderer.RenderCelebrationFrame(k.CompletedPhase, k.FrameIndex, totalFrames));

            return FromPngBytes(pngBytes);
        }
        catch
        {
            return new IconInfo("\uE73E"); // CheckMark glyph fallback
        }
    }

    private static IconInfo FromPngBytes(byte[] pngBytes)
    {
        var stream = new InMemoryRandomAccessStream();
        stream.WriteAsync(pngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
        stream.Seek(0);
        return IconInfo.FromStream(stream);
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
