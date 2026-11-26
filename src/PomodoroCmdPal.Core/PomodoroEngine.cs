namespace PomodoroCmdPal.Core;

public sealed class PomodoroEngine : IDisposable
{
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly IStatsStore? _statsStore;
    private readonly CancellationTokenSource _cts = new();

    private PomodoroConfig _config;
    private PomodoroPhase _phase = PomodoroPhase.Focus;
    private PomodoroState _state = PomodoroState.Idle;
    private TimeSpan _remaining;
    private TimeSpan _currentPhaseTotal;
    private int _completedFocusCycles;
    private string? _focusLabel;
    private DailyFocusStats _todayStats;
    private Task? _loopTask;
    private bool _disposed;

    public event Action<PomodoroSnapshot>? SnapshotChanged;
    public event Action<PomodoroPhase, PomodoroPhase>? PhaseCompleted;

    public PomodoroEngine(
        PomodoroConfig? config = null,
        TimeProvider? timeProvider = null,
        IStatsStore? statsStore = null,
        bool startBackgroundLoop = true)
    {
        _config = (config ?? PomodoroConfig.Classic).Sanitized();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _statsStore = statsStore;

        _currentPhaseTotal = _config.GetDuration(_phase);
        _remaining = _currentPhaseTotal;
        var today = GetToday();
        _todayStats = _statsStore?.Load(today) ?? DailyFocusStats.Empty(today);

        if (startBackgroundLoop)
        {
            _loopTask = Task.Run(() => RunTickLoopAsync(_cts.Token));
        }
    }

    public PomodoroConfig Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    public PomodoroSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            EnsureTodayStatsLocked();
            return CreateSnapshotLocked();
        }
    }

    public PomodoroSnapshot UpdateConfig(PomodoroConfig newConfig, bool applyToCurrentIfIdle = true)
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _config = newConfig.Sanitized();
            if (applyToCurrentIfIdle && _state == PomodoroState.Idle)
            {
                _currentPhaseTotal = _config.GetDuration(_phase);
                _remaining = _currentPhaseTotal;
            }

            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot SetFocusLabel(string? label)
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _focusLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot ClearFocusLabel() => SetFocusLabel(null);

    public PomodoroSnapshot StartCustomFocus(int minutes, string? label = null)
    {
        var clampedMinutes = Math.Clamp(minutes, 1, 180);
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _phase = PomodoroPhase.Focus;
            _currentPhaseTotal = TimeSpan.FromMinutes(clampedMinutes);
            _remaining = _currentPhaseTotal;
            _state = PomodoroState.Running;
            if (!string.IsNullOrWhiteSpace(label))
            {
                _focusLabel = label.Trim();
            }

            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot HandleSessionLocked()
    {
        PomodoroSnapshot? snapshot = null;
        lock (_lock)
        {
            if (_config.AutoPauseOnLock && _state == PomodoroState.Running && _phase == PomodoroPhase.Focus)
            {
                _state = PomodoroState.Paused;
                EnsureTodayStatsLocked();
                snapshot = CreateSnapshotLocked();
            }
        }

        if (snapshot is not null)
        {
            SnapshotChanged?.Invoke(snapshot);
            return snapshot;
        }

        return GetSnapshot();
    }

    public PomodoroSnapshot ToggleStartPause()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _state = _state == PomodoroState.Running
                ? PomodoroState.Paused
                : PomodoroState.Running;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot Start()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            if (_state == PomodoroState.Running)
            {
                EnsureTodayStatsLocked();
                return CreateSnapshotLocked();
            }

            _state = PomodoroState.Running;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot Pause()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            if (_state != PomodoroState.Running)
            {
                EnsureTodayStatsLocked();
                return CreateSnapshotLocked();
            }

            _state = PomodoroState.Paused;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot ExtendCurrentPhase(TimeSpan delta, bool startIfIdle = false)
    {
        if (delta <= TimeSpan.Zero)
        {
            return GetSnapshot();
        }

        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _remaining += delta;
            if (_remaining > _currentPhaseTotal)
            {
                _currentPhaseTotal = _remaining;
            }

            if (startIfIdle && _state == PomodoroState.Idle)
            {
                _state = PomodoroState.Running;
            }

            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot StartBonusFocus(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return GetSnapshot();
        }

        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _phase = PomodoroPhase.Focus;
            _currentPhaseTotal = duration;
            _remaining = duration;
            _state = PomodoroState.Running;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot ResetCurrentPhase()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _state = PomodoroState.Idle;
            _currentPhaseTotal = _config.GetDuration(_phase);
            _remaining = _currentPhaseTotal;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot SkipToNextPhase()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            AdvancePhaseLocked(autoStart: false, recordFocusCompletion: false);
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot SwitchPhase(PomodoroPhase phase, bool startImmediately = false)
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _phase = phase;
            _currentPhaseTotal = _config.GetDuration(phase);
            _remaining = _currentPhaseTotal;
            _state = startImmediately ? PomodoroState.Running : PomodoroState.Idle;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public PomodoroSnapshot ResetAll()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _phase = PomodoroPhase.Focus;
            _state = PomodoroState.Idle;
            _completedFocusCycles = 0;
            _currentPhaseTotal = _config.GetDuration(_phase);
            _remaining = _currentPhaseTotal;
            EnsureTodayStatsLocked();
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    public void TickOneSecond()
    {
        PomodoroSnapshot? snapshotToPublish = null;
        (PomodoroPhase Completed, PomodoroPhase Next)? completion = null;
        DailyFocusStats? statsToSave = null;

        lock (_lock)
        {
            if (_state != PomodoroState.Running)
            {
                return;
            }

            _remaining -= TimeSpan.FromSeconds(1);

            if (_remaining <= TimeSpan.Zero)
            {
                var completed = _phase;
                var completedMinutes = Math.Max(1, (int)Math.Round(_currentPhaseTotal.TotalMinutes));
                var next = AdvancePhaseLocked(
                    autoStart: _config.AutoStartNextPhase,
                    recordFocusCompletion: true,
                    completedFocusMinutes: completedMinutes);
                if (completed == PomodoroPhase.Focus)
                {
                    statsToSave = _todayStats;
                }

                completion = (completed, next);
            }

            EnsureTodayStatsLocked();
            snapshotToPublish = CreateSnapshotLocked();
        }

        if (statsToSave is not null)
        {
            _statsStore?.Save(statsToSave);
        }

        if (snapshotToPublish is not null)
        {
            SnapshotChanged?.Invoke(snapshotToPublish);
        }

        if (completion is { } c)
        {
            PhaseCompleted?.Invoke(c.Completed, c.Next);
        }
    }

    private PomodoroPhase AdvancePhaseLocked(bool autoStart, bool recordFocusCompletion, int completedFocusMinutes = 0)
    {
        EnsureTodayStatsLocked();

        if (_phase == PomodoroPhase.Focus)
        {
            if (recordFocusCompletion)
            {
                _todayStats = _todayStats.RecordFocusSession(GetToday(), completedFocusMinutes);
            }

            _completedFocusCycles++;
            _phase = _completedFocusCycles >= _config.CyclesBeforeLongBreak
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak;
        }
        else if (_phase == PomodoroPhase.LongBreak)
        {
            _completedFocusCycles = 0;
            _phase = PomodoroPhase.Focus;
        }
        else
        {
            _phase = PomodoroPhase.Focus;
        }

        _currentPhaseTotal = _config.GetDuration(_phase);
        _remaining = _currentPhaseTotal;
        _state = autoStart ? PomodoroState.Running : PomodoroState.Idle;
        return _phase;
    }

    private void EnsureTodayStatsLocked()
    {
        _todayStats = _todayStats.ForDate(GetToday());
    }

    private DateOnly GetToday() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private PomodoroSnapshot CreateSnapshotLocked() =>
        new(
            _phase,
            _state,
            _remaining,
            _currentPhaseTotal,
            _completedFocusCycles,
            _config.CyclesBeforeLongBreak,
            _todayStats,
            _focusLabel,
            _config.UseCycleDots);

    private async Task RunTickLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                TickOneSecond();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore cancellation/wait exceptions during shutdown.
        }

        _cts.Dispose();
    }
}
