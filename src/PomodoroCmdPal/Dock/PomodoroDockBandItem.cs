using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Commands;
using PomodoroCmdPal.Core;
using PomodoroCmdPal.Pages;

namespace PomodoroCmdPal.Dock;

internal sealed partial class PomodoroDockBandItem : CommandItem, IDisposable
{
    private readonly PomodoroEngine _engine;
    private readonly PomodoroPage _page;
    private readonly DockIconRenderer _iconRenderer = new();
    private readonly PomodoroActionCommand _toggleCommand;
    private (PomodoroPhase Phase, PomodoroState State, int Step)? _lastIconKey;

    public PomodoroDockBandItem(PomodoroEngine engine, PomodoroPage page)
    {
        _engine = engine;
        _page = page;

        _toggleCommand = new PomodoroActionCommand(
            name: "Start / Pause Pomodoro",
            icon: new IconInfo("\uE916"),
            action: () => _engine.ToggleStartPause(),
            id: "com.pomodorocmdpal.toggle");

        Command = _toggleCommand;
        RebuildContextMenu();

        ApplySnapshot(_engine.GetSnapshot());
        _engine.SnapshotChanged += ApplySnapshot;
    }

    public void RefreshContextMenuLabels()
    {
        RebuildContextMenu();
    }

    private void RebuildContextMenu()
    {
        var cfg = _engine.Config;
        MoreCommands =
        [
            new CommandContextItem(new PomodoroActionCommand(
                "+5 min",
                new IconInfo("\uE710"),
                () => _engine.ExtendCurrentPhase(TimeSpan.FromMinutes(5)),
                "com.pomodorocmdpal.extend5")),
            new CommandContextItem(new PomodoroActionCommand(
                "Reset Timer",
                new IconInfo("\uE72C"),
                () => _engine.ResetCurrentPhase(),
                "com.pomodorocmdpal.reset")),
            new CommandContextItem(new PomodoroActionCommand(
                "Skip to Next Phase",
                new IconInfo("\uE893"),
                () => _engine.SkipToNextPhase(),
                "com.pomodorocmdpal.skip")),
            new CommandContextItem(new PomodoroActionCommand(
                $"Focus ({cfg.FocusMinutes}m)",
                new IconInfo("\uE916"),
                () => _engine.SwitchPhase(PomodoroPhase.Focus, startImmediately: true),
                "com.pomodorocmdpal.focus")),
            new CommandContextItem(new PomodoroActionCommand(
                $"Short Break ({cfg.ShortBreakMinutes}m)",
                new IconInfo("\uEC32"),
                () => _engine.SwitchPhase(PomodoroPhase.ShortBreak, startImmediately: true),
                "com.pomodorocmdpal.shortbreak")),
            new CommandContextItem(new PomodoroActionCommand(
                $"Long Break ({cfg.LongBreakMinutes}m)",
                new IconInfo("\uE708"),
                () => _engine.SwitchPhase(PomodoroPhase.LongBreak, startImmediately: true),
                "com.pomodorocmdpal.longbreak")),
            new CommandContextItem(new PomodoroActionCommand(
                "Reset All Cycles",
                new IconInfo("\uE777"),
                () => _engine.ResetAll(),
                "com.pomodorocmdpal.resetall")),
            new CommandContextItem(_page)
            {
                Title = "Open Pomodoro Controls",
                Icon = new IconInfo("\uE713")
            }
        ];
    }

    private void ApplySnapshot(PomodoroSnapshot snapshot)
    {
        Title = snapshot.FormattedTime;
        Subtitle = snapshot.CompactSubtitle;
        _toggleCommand.Name = snapshot.State == PomodoroState.Running
            ? "Pause Pomodoro"
            : "Start Pomodoro";

        var visualKey = _iconRenderer.GetVisualKey(snapshot);
        if (_lastIconKey != visualKey)
        {
            _lastIconKey = visualKey;
            Icon = _iconRenderer.CreateIcon(snapshot);
        }
    }

    public void Dispose()
    {
        _engine.SnapshotChanged -= ApplySnapshot;
    }
}
