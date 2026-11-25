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
        Assert.Equal(1.0, snapshot.Progress, precision: 3);
        Assert.Equal(1, snapshot.CurrentCycleNumber);
        Assert.Equal("Focus • 1/4", snapshot.CompactSubtitle);
        Assert.Equal(0, snapshot.TodayStats.CompletedFocusSessions);
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
        Assert.True(afterTwoSeconds.Progress < 1.0 && afterTwoSeconds.Progress > 0.99);

        var paused = engine.ToggleStartPause();
        Assert.Equal(PomodoroState.Paused, paused.State);
        Assert.Equal("Paused • Focus", paused.CompactSubtitle);

        // Ticking while paused must not decrement remaining time.
        engine.TickOneSecond();
        Assert.Equal("24:58", engine.GetSnapshot().FormattedTime);
    }

    [Fact]
    public void ExtendCurrentPhase_AddsTimeAndExpandsTotalDuration()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);
        engine.Start();
        engine.TickOneSecond();

        var extended = engine.ExtendCurrentPhase(TimeSpan.FromMinutes(5));
        Assert.Equal("29:59", extended.FormattedTime);
        Assert.Equal(1.0, extended.Progress, precision: 3);
    }

    [Fact]
    public void StartBonusFocus_StartsImmediateFiveMinuteFocusSession()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);
        engine.SwitchPhase(PomodoroPhase.ShortBreak);

        var bonus = engine.StartBonusFocus(TimeSpan.FromMinutes(5));
        Assert.Equal(PomodoroPhase.Focus, bonus.Phase);
        Assert.Equal(PomodoroState.Running, bonus.State);
        Assert.Equal("05:00", bonus.FormattedTime);
    }

    [Fact]
    public void UpdateConfig_AppliesImmediatelyWhenIdleAndPreservesRunningTimer()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);

        var deep = engine.UpdateConfig(PomodoroConfig.DeepWork);
        Assert.Equal("50:00", deep.FormattedTime);

        engine.Start();
        engine.TickOneSecond();
        var whileRunning = engine.UpdateConfig(PomodoroConfig.Sprint);
        Assert.Equal("49:59", whileRunning.FormattedTime);
        Assert.Equal(15, engine.Config.FocusMinutes);
    }

    [Fact]
    public void FullFourCycleProgression_TracksDailyStatsAndLongBreak()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"pomo-stats-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonFileStatsStore(tempFile);
            var config = new PomodoroConfig(
                FocusMinutes: 1,
                ShortBreakMinutes: 1,
                LongBreakMinutes: 2,
                CyclesBeforeLongBreak: 4,
                AutoStartNextPhase: false);

            using var engine = new PomodoroEngine(config, statsStore: store, startBackgroundLoop: false);
            var completions = new List<(PomodoroPhase Completed, PomodoroPhase Next)>();
            engine.PhaseCompleted += (c, n) => completions.Add((c, n));

            for (var cycle = 1; cycle <= 3; cycle++)
            {
                Assert.Equal(PomodoroPhase.Focus, engine.GetSnapshot().Phase);
                Assert.Equal(cycle, engine.GetSnapshot().CurrentCycleNumber);

                engine.Start();
                for (var s = 0; s < 60; s++) engine.TickOneSecond();

                var afterFocus = engine.GetSnapshot();
                Assert.Equal(PomodoroPhase.ShortBreak, afterFocus.Phase);
                Assert.Equal(cycle, afterFocus.TodayStats.CompletedFocusSessions);
                Assert.Equal(cycle, afterFocus.TodayStats.TotalFocusMinutes);

                engine.Start();
                for (var s = 0; s < 60; s++) engine.TickOneSecond();
                Assert.Equal(PomodoroPhase.Focus, engine.GetSnapshot().Phase);
            }

            engine.Start();
            for (var s = 0; s < 60; s++) engine.TickOneSecond();

            var longBreak = engine.GetSnapshot();
            Assert.Equal(PomodoroPhase.LongBreak, longBreak.Phase);
            Assert.Equal(4, longBreak.TodayStats.CompletedFocusSessions);
            Assert.Equal("4 sessions • 4m focused", longBreak.TodayStats.FormattedSummary);

            // Verify persistence
            var reloaded = store.Load(longBreak.TodayStats.Date);
            Assert.Equal(4, reloaded.CompletedFocusSessions);
            Assert.Equal(4, reloaded.TotalFocusMinutes);

            engine.Start();
            for (var s = 0; s < 120; s++) engine.TickOneSecond();

            var freshFocus = engine.GetSnapshot();
            Assert.Equal(PomodoroPhase.Focus, freshFocus.Phase);
            Assert.Equal(0, freshFocus.CompletedFocusCycles);
            Assert.Equal(8, completions.Count);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Running, 0.75)]
    [InlineData(PomodoroPhase.ShortBreak, PomodoroState.Running, 0.5)]
    [InlineData(PomodoroPhase.LongBreak, PomodoroState.Running, 0.25)]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Paused, 0.4)]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Idle, 1.0)]
    public void ProgressRingPngRenderer_GeneratesValidPngSignature(PomodoroPhase phase, PomodoroState state, double progress)
    {
        var bytes = ProgressRingPngRenderer.Render(phase, state, progress);

        Assert.True(bytes.Length > 64);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x4E, bytes[2]); // 'N'
        Assert.Equal(0x47, bytes[3]); // 'G'
    }
}
