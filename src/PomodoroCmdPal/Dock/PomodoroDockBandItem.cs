using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Commands;
using PomodoroCmdPal.Core;
using PomodoroCmdPal.Pages;

namespace PomodoroCmdPal.Dock;

internal sealed partial class PomodoroDockBandItem : CommandItem, IDisposable
{
    private readonly PomodoroEngine _engine;
    private readonly PomodoroActionCommand _toggleCommand;
    private readonly IContextItem[] _contextItems;

    public PomodoroDockBandItem(PomodoroEngine engine, PomodoroPage page)
    {
        _engine = engine;

        _toggleCommand = new PomodoroActionCommand(
            name: "Start / Pause Pomodoro",
            icon: new IconInfo("\uE916"),
            action: () => _engine.ToggleStartPause(),
            id: "com.pomodorocmdpal.toggle");

        Command = _toggleCommand;

        _contextItems =
        [
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
                "Focus (25m)",
                new IconInfo("\uE916"),
                () => _engine.SwitchPhase(PomodoroPhase.Focus, startImmediately: true),
                "com.pomodorocmdpal.focus")),
            new CommandContextItem(new PomodoroActionCommand(
                "Short Break (5m)",
                new IconInfo("\uEC32"),
                () => _engine.SwitchPhase(PomodoroPhase.ShortBreak, startImmediately: true),
                "com.pomodorocmdpal.shortbreak")),
            new CommandContextItem(new PomodoroActionCommand(
                "Long Break (15m)",
                new IconInfo("\uE708"),
                () => _engine.SwitchPhase(PomodoroPhase.LongBreak, startImmediately: true),
                "com.pomodorocmdpal.longbreak")),
            new CommandContextItem(new PomodoroActionCommand(
                "Reset All Cycles",
                new IconInfo("\uE777"),
                () => _engine.ResetAll(),
                "com.pomodorocmdpal.resetall")),
            new CommandContextItem(page)
            {
                Title = "Open Pomodoro Controls",
                Icon = new IconInfo("\uE713")
            }
        ];

        MoreCommands = _contextItems;

        ApplySnapshot(_engine.GetSnapshot());
        _engine.SnapshotChanged += ApplySnapshot;
    }

    private void ApplySnapshot(PomodoroSnapshot snapshot)
    {
        Title = snapshot.FormattedTime;
        Subtitle = snapshot.CompactSubtitle;
        Icon = GetIconForSnapshot(snapshot);
        _toggleCommand.Name = snapshot.State == PomodoroState.Running
            ? "Pause Pomodoro"
            : "Start Pomodoro";
    }

    private static IconInfo GetIconForSnapshot(PomodoroSnapshot snapshot) => snapshot.State switch
    {
        PomodoroState.Running => snapshot.Phase == PomodoroPhase.Focus
            ? new IconInfo("\uE769") // Pause glyph while actively running Focus
            : new IconInfo("\uEC32"), // Coffee/break glyph while running Break
        PomodoroState.Paused => new IconInfo("\uE768"), // Play glyph while paused
        _ => new IconInfo("\uE916") // Stopwatch glyph while idle
    };

    public void Dispose()
    {
        _engine.SnapshotChanged -= ApplySnapshot;
    }
}
