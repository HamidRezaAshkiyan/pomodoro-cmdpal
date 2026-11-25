namespace PomodoroCmdPal.Core;

public enum PomodoroPhase
{
    Focus,
    ShortBreak,
    LongBreak
}

public enum PomodoroState
{
    Idle,
    Running,
    Paused
}

public sealed record PomodoroConfig(
    int FocusMinutes = 25,
    int ShortBreakMinutes = 5,
    int LongBreakMinutes = 15,
    int CyclesBeforeLongBreak = 4,
    bool AutoStartNextPhase = false)
{
    public TimeSpan GetDuration(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => TimeSpan.FromMinutes(FocusMinutes),
        PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(ShortBreakMinutes),
        PomodoroPhase.LongBreak => TimeSpan.FromMinutes(LongBreakMinutes),
        _ => TimeSpan.FromMinutes(FocusMinutes)
    };
}

public sealed record PomodoroSnapshot(
    PomodoroPhase Phase,
    PomodoroState State,
    TimeSpan Remaining,
    int CompletedFocusCycles,
    int CyclesBeforeLongBreak)
{
    /// <summary>
    /// 1-based cycle index shown in the UI (e.g., 1/4 .. 4/4).
    /// </summary>
    public int CurrentCycleNumber => Phase switch
    {
        PomodoroPhase.Focus => Math.Min(CompletedFocusCycles + 1, CyclesBeforeLongBreak),
        PomodoroPhase.ShortBreak => Math.Clamp(CompletedFocusCycles, 1, CyclesBeforeLongBreak),
        PomodoroPhase.LongBreak => CyclesBeforeLongBreak,
        _ => 1
    };

    public string PhaseLabel => Phase switch
    {
        PomodoroPhase.Focus => "Focus",
        PomodoroPhase.ShortBreak => "Short Break",
        PomodoroPhase.LongBreak => "Long Break",
        _ => "Focus"
    };

    /// <summary>
    /// Minimal MM:SS display string for the Dock title.
    /// </summary>
    public string FormattedTime
    {
        get
        {
            var clamped = Remaining < TimeSpan.Zero ? TimeSpan.Zero : Remaining;
            var totalMinutes = (int)clamped.TotalMinutes;
            return $"{totalMinutes:00}:{clamped.Seconds:00}";
        }
    }

    /// <summary>
    /// Compact subtitle for the Dock band (e.g., "Focus • 1/4", "Paused • Focus").
    /// </summary>
    public string CompactSubtitle => State switch
    {
        PomodoroState.Paused => $"Paused • {PhaseLabel}",
        PomodoroState.Running => $"{PhaseLabel} • {CurrentCycleNumber}/{CyclesBeforeLongBreak}",
        _ => $"{PhaseLabel} • {CurrentCycleNumber}/{CyclesBeforeLongBreak}"
    };
}
