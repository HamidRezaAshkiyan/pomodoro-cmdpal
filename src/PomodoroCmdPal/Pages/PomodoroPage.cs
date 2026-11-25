using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Commands;
using PomodoroCmdPal.Core;

namespace PomodoroCmdPal.Pages;

internal sealed partial class PomodoroPage : ListPage, IDisposable
{
    private readonly PomodoroEngine _engine;
    private readonly Action<PomodoroConfig> _applyPreset;

    public PomodoroPage(PomodoroEngine engine, Action<PomodoroConfig> applyPreset)
    {
        _engine = engine;
        _applyPreset = applyPreset;
        Id = "com.pomodorocmdpal.page";
        Name = "Pomodoro Timer";
        Title = "Pomodoro Timer";
        Icon = new IconInfo("\uE916");

        _engine.SnapshotChanged += OnSnapshotChanged;
    }

    public override IListItem[] GetItems()
    {
        var snapshot = _engine.GetSnapshot();
        var cfg = _engine.Config;
        var toggleLabel = snapshot.State == PomodoroState.Running ? "Pause Timer" : "Start Timer";
        var toggleGlyph = snapshot.State == PomodoroState.Running ? "\uE769" : "\uE768";

        return
        [
            new ListItem(new PomodoroActionCommand(toggleLabel, new IconInfo(toggleGlyph), () => _engine.ToggleStartPause()))
            {
                Title = $"{snapshot.FormattedTime} — {toggleLabel}",
                Subtitle = $"{snapshot.CompactSubtitle}  |  Today: {snapshot.TodayStats.FormattedSummary}",
                Icon = new IconInfo(toggleGlyph)
            },
            new ListItem(new PomodoroActionCommand("+5 min", new IconInfo("\uE710"), () => _engine.ExtendCurrentPhase(TimeSpan.FromMinutes(5))))
            {
                Title = "+5 Minutes",
                Subtitle = $"Extend current {snapshot.PhaseLabel} without breaking flow",
                Icon = new IconInfo("\uE710")
            },
            new ListItem(new PomodoroActionCommand("Reset Current Phase", new IconInfo("\uE72C"), () => _engine.ResetCurrentPhase()))
            {
                Title = "Reset Current Phase",
                Subtitle = $"Reset {snapshot.PhaseLabel} back to full duration",
                Icon = new IconInfo("\uE72C")
            },
            new ListItem(new PomodoroActionCommand("Skip to Next Phase", new IconInfo("\uE893"), () => _engine.SkipToNextPhase()))
            {
                Title = "Skip to Next Phase",
                Subtitle = "Advance immediately to the next Pomodoro interval",
                Icon = new IconInfo("\uE893")
            },
            new ListItem(new PomodoroActionCommand($"Start Focus ({cfg.FocusMinutes}m)", new IconInfo("\uE916"), () => _engine.SwitchPhase(PomodoroPhase.Focus, startImmediately: true)))
            {
                Title = $"Start Focus ({cfg.FocusMinutes:00}:00)",
                Subtitle = $"Switch to a {cfg.FocusMinutes}-minute focus session",
                Icon = new IconInfo("\uE916")
            },
            new ListItem(new PomodoroActionCommand($"Start Short Break ({cfg.ShortBreakMinutes}m)", new IconInfo("\uEC32"), () => _engine.SwitchPhase(PomodoroPhase.ShortBreak, startImmediately: true)))
            {
                Title = $"Start Short Break ({cfg.ShortBreakMinutes:00}:00)",
                Subtitle = $"Switch to a {cfg.ShortBreakMinutes}-minute short break",
                Icon = new IconInfo("\uEC32")
            },
            new ListItem(new PomodoroActionCommand($"Start Long Break ({cfg.LongBreakMinutes}m)", new IconInfo("\uE708"), () => _engine.SwitchPhase(PomodoroPhase.LongBreak, startImmediately: true)))
            {
                Title = $"Start Long Break ({cfg.LongBreakMinutes:00}:00)",
                Subtitle = $"Switch to a {cfg.LongBreakMinutes}-minute long break",
                Icon = new IconInfo("\uE708")
            },
            new ListItem(new PomodoroActionCommand("Preset: Classic (25/5/15)", new IconInfo("\uE823"), () => _applyPreset(PomodoroConfig.Classic)))
            {
                Title = "Preset: Classic (25 / 5 / 15)",
                Subtitle = "25m Focus, 5m Short Break, 15m Long Break",
                Icon = new IconInfo("\uE823")
            },
            new ListItem(new PomodoroActionCommand("Preset: Deep Work (50/10/20)", new IconInfo("\uE7BE"), () => _applyPreset(PomodoroConfig.DeepWork)))
            {
                Title = "Preset: Deep Work (50 / 10 / 20)",
                Subtitle = "50m Focus, 10m Short Break, 20m Long Break",
                Icon = new IconInfo("\uE7BE")
            },
            new ListItem(new PomodoroActionCommand("Preset: Sprint (15/3/10)", new IconInfo("\uE945"), () => _applyPreset(PomodoroConfig.Sprint)))
            {
                Title = "Preset: Sprint (15 / 3 / 10)",
                Subtitle = "15m Focus, 3m Short Break, 10m Long Break",
                Icon = new IconInfo("\uE945")
            },
            new ListItem(new PomodoroActionCommand("Reset All Cycles", new IconInfo("\uE777"), () => _engine.ResetAll()))
            {
                Title = "Reset All Cycles",
                Subtitle = $"Reset timer to Focus ({cfg.FocusMinutes:00}:00) and cycle 1/{cfg.CyclesBeforeLongBreak}",
                Icon = new IconInfo("\uE777")
            }
        ];
    }

    private void OnSnapshotChanged(PomodoroSnapshot _)
    {
        RaiseItemsChanged();
    }

    public void Dispose()
    {
        _engine.SnapshotChanged -= OnSnapshotChanged;
    }
}
