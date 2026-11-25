using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Commands;
using PomodoroCmdPal.Core;

namespace PomodoroCmdPal.Pages;

internal sealed partial class PomodoroPage : ListPage, IDisposable
{
    private readonly PomodoroEngine _engine;

    public PomodoroPage(PomodoroEngine engine)
    {
        _engine = engine;
        Id = "com.pomodorocmdpal.page";
        Name = "Pomodoro Timer";
        Title = "Pomodoro Timer";
        Icon = new IconInfo("\uE916");

        _engine.SnapshotChanged += OnSnapshotChanged;
    }

    public override IListItem[] GetItems()
    {
        var snapshot = _engine.GetSnapshot();
        var toggleLabel = snapshot.State == PomodoroState.Running ? "Pause Timer" : "Start Timer";
        var toggleGlyph = snapshot.State == PomodoroState.Running ? "\uE769" : "\uE768";

        return
        [
            new ListItem(new PomodoroActionCommand(toggleLabel, new IconInfo(toggleGlyph), () => _engine.ToggleStartPause()))
            {
                Title = $"{snapshot.FormattedTime} — {toggleLabel}",
                Subtitle = snapshot.CompactSubtitle,
                Icon = new IconInfo(toggleGlyph)
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
            new ListItem(new PomodoroActionCommand("Start Focus (25m)", new IconInfo("\uE916"), () => _engine.SwitchPhase(PomodoroPhase.Focus, startImmediately: true)))
            {
                Title = "Start Focus (25:00)",
                Subtitle = "Switch to a 25-minute focus session",
                Icon = new IconInfo("\uE916")
            },
            new ListItem(new PomodoroActionCommand("Start Short Break (5m)", new IconInfo("\uEC32"), () => _engine.SwitchPhase(PomodoroPhase.ShortBreak, startImmediately: true)))
            {
                Title = "Start Short Break (05:00)",
                Subtitle = "Switch to a 5-minute short break",
                Icon = new IconInfo("\uEC32")
            },
            new ListItem(new PomodoroActionCommand("Start Long Break (15m)", new IconInfo("\uE708"), () => _engine.SwitchPhase(PomodoroPhase.LongBreak, startImmediately: true)))
            {
                Title = "Start Long Break (15:00)",
                Subtitle = "Switch to a 15-minute long break",
                Icon = new IconInfo("\uE708")
            },
            new ListItem(new PomodoroActionCommand("Reset All Cycles", new IconInfo("\uE777"), () => _engine.ResetAll()))
            {
                Title = "Reset All Cycles",
                Subtitle = "Reset timer to Focus 25:00 and cycle 1/4",
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
