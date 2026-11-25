using PomodoroCmdPal.Core;
using Xunit;

namespace PomodoroCmdPal.Core.Tests;

public sealed class PomodoroEngineTests
{
    [Fact]
    public void InitialState_IsClassicFocus25MinutesIdleCycle1Of4()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);
        var snapshot = engine.GetSnapshot();

        Assert.Equal(PomodoroPhase.Focus, snapshot.Phase);
        Assert.Equal(PomodoroState.Idle, snapshot.State);
        Assert.Equal("25:00", snapshot.FormattedTime);
        Assert.Equal(1, snapshot.CurrentCycleNumber);
        Assert.Equal("Focus • 1/4", snapshot.CompactSubtitle);
    }

    [Fact]
    public void ToggleStartPause_StartsTicksAndPausesCleanly()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);

        var running = engine.ToggleStartPause();
        Assert.Equal(PomodoroState.Running, running.State);

        engine.TickOneSecond();
        engine.TickOneSecond();

        var afterTwoSeconds = engine.GetSnapshot();
        Assert.Equal("24:58", afterTwoSeconds.FormattedTime);

        var paused = engine.ToggleStartPause();
        Assert.Equal(PomodoroState.Paused, paused.State);
        Assert.Equal("Paused • Focus", paused.CompactSubtitle);

        // Ticking while paused must not decrement remaining time.
        engine.TickOneSecond();
        Assert.Equal("24:58", engine.GetSnapshot().FormattedTime);
    }

    [Fact]
    public void ResetCurrentPhase_RestoresFullDurationAndIdleState()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);
        engine.Start();
        engine.TickOneSecond();
        engine.TickOneSecond();

        var reset = engine.ResetCurrentPhase();
        Assert.Equal(PomodoroState.Idle, reset.State);
        Assert.Equal("25:00", reset.FormattedTime);
    }

    [Fact]
    public void FullFourCycleProgression_TriggersShortBreaksThenLongBreakThenResets()
    {
        var config = new PomodoroConfig(
            FocusMinutes: 1,
            ShortBreakMinutes: 1,
            LongBreakMinutes: 2,
            CyclesBeforeLongBreak: 4,
            AutoStartNextPhase: false);

        using var engine = new PomodoroEngine(config, startBackgroundLoop: false);
        var completions = new List<(PomodoroPhase Completed, PomodoroPhase Next)>();
        engine.PhaseCompleted += (c, n) => completions.Add((c, n));

        // Cycles 1..3: Focus -> ShortBreak -> Focus
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            Assert.Equal(PomodoroPhase.Focus, engine.GetSnapshot().Phase);
            Assert.Equal(cycle, engine.GetSnapshot().CurrentCycleNumber);

            engine.Start();
            for (var s = 0; s < 60; s++) engine.TickOneSecond();

            var afterFocus = engine.GetSnapshot();
            Assert.Equal(PomodoroPhase.ShortBreak, afterFocus.Phase);
            Assert.Equal(PomodoroState.Idle, afterFocus.State);
            Assert.Equal("01:00", afterFocus.FormattedTime);

            engine.Start();
            for (var s = 0; s < 60; s++) engine.TickOneSecond();
            Assert.Equal(PomodoroPhase.Focus, engine.GetSnapshot().Phase);
        }

        // Cycle 4: Focus -> LongBreak -> Focus (reset to cycle 1)
        Assert.Equal(4, engine.GetSnapshot().CurrentCycleNumber);
        engine.Start();
        for (var s = 0; s < 60; s++) engine.TickOneSecond();

        var longBreak = engine.GetSnapshot();
        Assert.Equal(PomodoroPhase.LongBreak, longBreak.Phase);
        Assert.Equal("02:00", longBreak.FormattedTime);

        engine.Start();
        for (var s = 0; s < 120; s++) engine.TickOneSecond();

        var freshFocus = engine.GetSnapshot();
        Assert.Equal(PomodoroPhase.Focus, freshFocus.Phase);
        Assert.Equal(0, freshFocus.CompletedFocusCycles);
        Assert.Equal(1, freshFocus.CurrentCycleNumber);
        Assert.Equal(8, completions.Count);
        Assert.Equal((PomodoroPhase.Focus, PomodoroPhase.LongBreak), completions[6]);
        Assert.Equal((PomodoroPhase.LongBreak, PomodoroPhase.Focus), completions[7]);
    }

    [Fact]
    public void SwitchPhaseAndSkip_UpdateStateImmediately()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);

        var shortBreak = engine.SwitchPhase(PomodoroPhase.ShortBreak, startImmediately: true);
        Assert.Equal(PomodoroPhase.ShortBreak, shortBreak.Phase);
        Assert.Equal(PomodoroState.Running, shortBreak.State);
        Assert.Equal("05:00", shortBreak.FormattedTime);

        var skipped = engine.SkipToNextPhase();
        Assert.Equal(PomodoroPhase.Focus, skipped.Phase);
        Assert.Equal(PomodoroState.Idle, skipped.State);
        Assert.Equal("25:00", skipped.FormattedTime);
    }
}
