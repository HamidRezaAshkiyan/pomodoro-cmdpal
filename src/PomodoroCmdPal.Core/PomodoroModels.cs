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
    bool AutoStartNextPhase = false,
    bool EnableSound = true)
{
    public static PomodoroConfig Classic => new(25, 5, 15, 4);
    public static PomodoroConfig DeepWork => new(50, 10, 20, 4);
    public static PomodoroConfig Sprint => new(15, 3, 10, 4);

    public PomodoroConfig Sanitized() => new(
        FocusMinutes: Math.Clamp(FocusMinutes, 1, 180),
        ShortBreakMinutes: Math.Clamp(ShortBreakMinutes, 1, 60),
        LongBreakMinutes: Math.Clamp(LongBreakMinutes, 1, 90),
        CyclesBeforeLongBreak: Math.Clamp(CyclesBeforeLongBreak, 1, 12),
        AutoStartNextPhase: AutoStartNextPhase,
        EnableSound: EnableSound);

    public TimeSpan GetDuration(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => TimeSpan.FromMinutes(Math.Clamp(FocusMinutes, 1, 180)),
        PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(Math.Clamp(ShortBreakMinutes, 1, 60)),
        PomodoroPhase.LongBreak => TimeSpan.FromMinutes(Math.Clamp(LongBreakMinutes, 1, 90)),
        _ => TimeSpan.FromMinutes(Math.Clamp(FocusMinutes, 1, 180))
    };
}

public sealed record DailyFocusStats(
    DateOnly Date,
    int CompletedFocusSessions,
    int TotalFocusMinutes)
{
    public static DailyFocusStats Empty(DateOnly date) => new(date, 0, 0);

    public string FormattedSummary
    {
        get
        {
            if (CompletedFocusSessions <= 0)
            {
                return "No focus sessions yet today";
            }

            var sessionWord = CompletedFocusSessions == 1 ? "session" : "sessions";
            var hours = TotalFocusMinutes / 60;
            var mins = TotalFocusMinutes % 60;
            var duration = hours > 0
                ? (mins > 0 ? $"{hours}h {mins}m" : $"{hours}h")
                : $"{mins}m";

            return $"{CompletedFocusSessions} {sessionWord} • {duration} focused";
        }
    }
}

public sealed record PomodoroSnapshot(
    PomodoroPhase Phase,
    PomodoroState State,
    TimeSpan Remaining,
    TimeSpan TotalPhaseDuration,
    int CompletedFocusCycles,
    int CyclesBeforeLongBreak,
    DailyFocusStats TodayStats)
{
    /// <summary>
    /// Remaining fraction from 0.0 to 1.0 for the Dock progress ring.
    /// </summary>
    public double Progress => TotalPhaseDuration.TotalSeconds <= 0
        ? 0.0
        : Math.Clamp(Remaining.TotalSeconds / TotalPhaseDuration.TotalSeconds, 0.0, 1.0);

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
        _ => $"{PhaseLabel} • {CurrentCycleNumber}/{CyclesBeforeLongBreak}"
    };
}
