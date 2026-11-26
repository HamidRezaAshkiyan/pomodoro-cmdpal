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
    bool EnableSound = true,
    bool UseCycleDots = true,
    bool AutoPauseOnLock = true)
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
        EnableSound: EnableSound,
        UseCycleDots: UseCycleDots,
        AutoPauseOnLock: AutoPauseOnLock);

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
    int TotalFocusMinutes,
    int CurrentStreakDays = 0,
    DateOnly? LastCompletedDate = null)
{
    public static DailyFocusStats Empty(DateOnly date) => new(date, 0, 0, 0, null);

    public DailyFocusStats ForDate(DateOnly today)
    {
        if (Date == today)
        {
            return this;
        }

        var streakAlive = LastCompletedDate.HasValue && LastCompletedDate.Value >= today.AddDays(-1);
        return new DailyFocusStats(
            Date: today,
            CompletedFocusSessions: 0,
            TotalFocusMinutes: 0,
            CurrentStreakDays: streakAlive ? CurrentStreakDays : 0,
            LastCompletedDate: streakAlive ? LastCompletedDate : null);
    }

    public DailyFocusStats RecordFocusSession(DateOnly today, int minutes)
    {
        var current = ForDate(today);
        var nextStreak = current.LastCompletedDate switch
        {
            null => 1,
            var last when last == today => Math.Max(1, current.CurrentStreakDays),
            var last when last == today.AddDays(-1) => current.CurrentStreakDays + 1,
            _ => 1
        };

        return new DailyFocusStats(
            Date: today,
            CompletedFocusSessions: current.CompletedFocusSessions + 1,
            TotalFocusMinutes: current.TotalFocusMinutes + Math.Max(1, minutes),
            CurrentStreakDays: nextStreak,
            LastCompletedDate: today);
    }

    public string FormattedSummary
    {
        get
        {
            var streakSuffix = CurrentStreakDays >= 2 ? $" • {CurrentStreakDays}-day streak" : string.Empty;

            if (CompletedFocusSessions <= 0)
            {
                return CurrentStreakDays >= 2
                    ? $"0 sessions today • {CurrentStreakDays}-day streak"
                    : "No focus sessions yet today";
            }

            var sessionWord = CompletedFocusSessions == 1 ? "session" : "sessions";
            var hours = TotalFocusMinutes / 60;
            var mins = TotalFocusMinutes % 60;
            var duration = hours > 0
                ? (mins > 0 ? $"{hours}h {mins}m" : $"{hours}h")
                : $"{mins}m";

            return $"{CompletedFocusSessions} {sessionWord} • {duration}{streakSuffix}";
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
    DailyFocusStats TodayStats,
    string? FocusLabel = null,
    bool UseCycleDots = true)
{
    public double Progress => TotalPhaseDuration.TotalSeconds <= 0
        ? 0.0
        : Math.Clamp(Remaining.TotalSeconds / TotalPhaseDuration.TotalSeconds, 0.0, 1.0);

    public bool IsFinalMinute => State == PomodoroState.Running
        && Remaining > TimeSpan.Zero
        && Remaining <= TimeSpan.FromSeconds(60);

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

    public string ActiveDisplayLabel =>
        Phase == PomodoroPhase.Focus && !string.IsNullOrWhiteSpace(FocusLabel)
            ? TruncateLabel(FocusLabel!, 18)
            : PhaseLabel;

    public string CycleIndicator
    {
        get
        {
            if (!UseCycleDots || CyclesBeforeLongBreak > 6)
            {
                return $"{CurrentCycleNumber}/{CyclesBeforeLongBreak}";
            }

            var filled = Math.Clamp(CurrentCycleNumber, 1, CyclesBeforeLongBreak);
            return new string('●', filled) + new string('○', CyclesBeforeLongBreak - filled);
        }
    }

    public string FormattedTime
    {
        get
        {
            var clamped = Remaining < TimeSpan.Zero ? TimeSpan.Zero : Remaining;
            var totalMinutes = (int)clamped.TotalMinutes;
            return $"{totalMinutes:00}:{clamped.Seconds:00}";
        }
    }

    public string CompactSubtitle => State switch
    {
        PomodoroState.Paused => $"Paused • {ActiveDisplayLabel}",
        _ => $"{ActiveDisplayLabel} • {CycleIndicator}"
    };

    private static string TruncateLabel(string value, int maxLen)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..(maxLen - 1)] + "…";
    }
}
