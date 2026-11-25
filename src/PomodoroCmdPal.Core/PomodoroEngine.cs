namespace PomodoroCmdPal.Core;

public sealed class PomodoroEngine : IDisposable
{
    private readonly object _lock = new();
    private readonly PomodoroConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _cts = new();

    private PomodoroPhase _phase = PomodoroPhase.Focus;
    private PomodoroState _state = PomodoroState.Idle;
    private TimeSpan _remaining;
    private int _completedFocusCycles;
    private Task? _loopTask;
    private bool _disposed;

    public event Action<PomodoroSnapshot>? SnapshotChanged;
    public event Action<PomodoroPhase, PomodoroPhase>? PhaseCompleted;

    public PomodoroEngine(PomodoroConfig? config = null, TimeProvider? timeProvider = null, bool startBackgroundLoop = true)
    {
        _config = config ?? new PomodoroConfig();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _remaining = _config.GetDuration(_phase);

        if (startBackgroundLoop)
        {
            _loopTask = Task.Run(() => RunTickLoopAsync(_cts.Token));
        }
    }

    public PomodoroConfig Config => _config;

    public PomodoroSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return CreateSnapshotLocked();
        }
    }

    public PomodoroSnapshot ToggleStartPause()
    {
        PomodoroSnapshot snapshot;
        lock (_lock)
        {
            _state = _state == PomodoroState.Running
                ? PomodoroState.Paused
                : PomodoroState.Running;
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
                return CreateSnapshotLocked();
            }

            _state = PomodoroState.Running;
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
                return CreateSnapshotLocked();
            }

            _state = PomodoroState.Paused;
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
            _remaining = _config.GetDuration(_phase);
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
            AdvancePhaseLocked(autoStart: false);
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
            _remaining = _config.GetDuration(phase);
            _state = startImmediately ? PomodoroState.Running : PomodoroState.Idle;
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
            _remaining = _config.GetDuration(_phase);
            snapshot = CreateSnapshotLocked();
        }

        SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Advances the timer by one second if currently running.
    /// Exposed internally/publicly for deterministic unit testing.
    /// </summary>
    public void TickOneSecond()
    {
        PomodoroSnapshot? snapshotToPublish = null;
        (PomodoroPhase Completed, PomodoroPhase Next)? completion = null;

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
                var next = AdvancePhaseLocked(autoStart: _config.AutoStartNextPhase);
                completion = (completed, next);
            }

            snapshotToPublish = CreateSnapshotLocked();
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

    private PomodoroPhase AdvancePhaseLocked(bool autoStart)
    {
        if (_phase == PomodoroPhase.Focus)
        {
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

        _remaining = _config.GetDuration(_phase);
        _state = autoStart ? PomodoroState.Running : PomodoroState.Idle;
        return _phase;
    }

    private PomodoroSnapshot CreateSnapshotLocked() =>
        new(_phase, _state, _remaining, _completedFocusCycles, _config.CyclesBeforeLongBreak);

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
