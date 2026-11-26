using PomodoroCmdPal.Core;
using Xunit;

namespace PomodoroCmdPal.Core.Tests;

public sealed class PomodoroEngineTests
{
    [Fact]
    public void InitialState_IsClassicFocus25MinutesIdleWithCycleDots()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);
        var snapshot = engine.GetSnapshot();

        Assert.Equal(PomodoroPhase.Focus, snapshot.Phase);
        Assert.Equal(PomodoroState.Idle, snapshot.State);
        Assert.Equal("25:00", snapshot.FormattedTime);
        Assert.Equal(1.0, snapshot.Progress, precision: 3);
        Assert.Equal(1, snapshot.CurrentCycleNumber);
        Assert.Equal("Focus • ●○○○", snapshot.CompactSubtitle);
        Assert.Equal(0, snapshot.TodayStats.CompletedFocusSessions);
    }

    [Fact]
    public void CycleDotsToggle_SwitchesBetweenDotsAndNumbers()
    {
        using var engine = new PomodoroEngine(new PomodoroConfig(UseCycleDots: false), startBackgroundLoop: false);
        Assert.Equal("Focus • 1/4", engine.GetSnapshot().CompactSubtitle);

        engine.UpdateConfig(new PomodoroConfig(UseCycleDots: true));
        Assert.Equal("Focus • ●○○○", engine.GetSnapshot().CompactSubtitle);
    }

    [Fact]
    public void FocusLabelAndCustomDuration_UpdateDockSubtitleAndTimer()
    {
        using var engine = new PomodoroEngine(startBackgroundLoop: false);

        var custom = engine.StartCustomFocus(12, "API refactor");
        Assert.Equal(PomodoroPhase.Focus, custom.Phase);
        Assert.Equal(PomodoroState.Running, custom.State);
        Assert.Equal("12:00", custom.FormattedTime);
        Assert.Equal("API refactor • ●○○○", custom.CompactSubtitle);

        var paused = engine.Pause();
        Assert.Equal("Paused • API refactor", paused.CompactSubtitle);

        var cleared = engine.ClearFocusLabel();
        Assert.Null(cleared.FocusLabel);
        Assert.Equal("Paused • Focus", cleared.CompactSubtitle);
    }

    [Fact]
    public void HandleSessionLocked_AutoPausesRunningFocusOnlyWhenEnabled()
    {
        using var engine = new PomodoroEngine(new PomodoroConfig(AutoPauseOnLock: true), startBackgroundLoop: false);
        engine.Start();
        Assert.Equal(PomodoroState.Running, engine.GetSnapshot().State);

        var afterLock = engine.HandleSessionLocked();
        Assert.Equal(PomodoroState.Paused, afterLock.State);

        // When disabled, locking does not pause
        engine.UpdateConfig(new PomodoroConfig(AutoPauseOnLock: false));
        engine.Start();
        Assert.Equal(PomodoroState.Running, engine.HandleSessionLocked().State);
    }

    [Fact]
    public void FinalMinute_SetsIsFinalMinuteFlagDuringLast60Seconds()
    {
        using var engine = new PomodoroEngine(new PomodoroConfig(FocusMinutes: 2), startBackgroundLoop: false);
        engine.Start();
        Assert.False(engine.GetSnapshot().IsFinalMinute);

        for (var i = 0; i < 60; i++) engine.TickOneSecond();
        Assert.True(engine.GetSnapshot().IsFinalMinute);
        Assert.Equal("01:00", engine.GetSnapshot().FormattedTime);
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
    public void DailyFocusStats_TracksConsecutiveDayStreaksAndResetsOnGap()
    {
        var day1 = new DateOnly(2026, 11, 20);
        var stats = DailyFocusStats.Empty(day1)
            .RecordFocusSession(day1, 25);

        Assert.Equal(1, stats.CurrentStreakDays);
        Assert.Equal(1, stats.CompletedFocusSessions);

        var day2 = day1.AddDays(1);
        stats = stats.RecordFocusSession(day2, 25);
        Assert.Equal(2, stats.CurrentStreakDays);
        Assert.Equal(1, stats.CompletedFocusSessions);
        Assert.Contains("2-day streak", stats.FormattedSummary);

        var day4 = day2.AddDays(2); // skipped day3
        stats = stats.ForDate(day4);
        Assert.Equal(0, stats.CurrentStreakDays);
        Assert.Equal(0, stats.CompletedFocusSessions);
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
                engine.Start();
                for (var s = 0; s < 60; s++) engine.TickOneSecond();
                Assert.Equal(PomodoroPhase.ShortBreak, engine.GetSnapshot().Phase);

                engine.Start();
                for (var s = 0; s < 60; s++) engine.TickOneSecond();
                Assert.Equal(PomodoroPhase.Focus, engine.GetSnapshot().Phase);
            }

            engine.Start();
            for (var s = 0; s < 60; s++) engine.TickOneSecond();

            var longBreak = engine.GetSnapshot();
            Assert.Equal(PomodoroPhase.LongBreak, longBreak.Phase);
            Assert.Equal(4, longBreak.TodayStats.CompletedFocusSessions);
            Assert.Equal(1, longBreak.TodayStats.CurrentStreakDays);

            var reloaded = store.Load(longBreak.TodayStats.Date);
            Assert.Equal(4, reloaded.CompletedFocusSessions);
            Assert.Equal(1, reloaded.CurrentStreakDays);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Running, 0.75, false, false)]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Running, 0.03, true, true)]
    [InlineData(PomodoroPhase.ShortBreak, PomodoroState.Running, 0.5, false, false)]
    [InlineData(PomodoroPhase.LongBreak, PomodoroState.Running, 0.25, false, false)]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Paused, 0.4, false, false)]
    [InlineData(PomodoroPhase.Focus, PomodoroState.Idle, 1.0, false, false)]
    public void ProgressRingPngRenderer_GeneratesValidPngSignature(
        PomodoroPhase phase, PomodoroState state, double progress, bool pulseHigh, bool isFinalMinute)
    {
        var bytes = ProgressRingPngRenderer.Render(phase, state, progress, pulseHigh, isFinalMinute);

        Assert.True(bytes.Length > 64);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Theory]
    [InlineData(PomodoroPhase.Focus, 0)]
    [InlineData(PomodoroPhase.Focus, 2)]
    [InlineData(PomodoroPhase.Focus, 4)]
    [InlineData(PomodoroPhase.ShortBreak, 2)]
    public void ProgressRingPngRenderer_CelebrationFramesGenerateValidPng(PomodoroPhase completedPhase, int frame)
    {
        var bytes = ProgressRingPngRenderer.RenderCelebrationFrame(completedPhase, frame, totalFrames: 5);

        Assert.True(bytes.Length > 64);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }
}
