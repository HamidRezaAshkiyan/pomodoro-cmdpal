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
    private readonly object _animLock = new();

    private (PomodoroPhase Phase, PomodoroState State, int Step, int MaxSteps, bool PulseHigh, bool IsFinalMinute)? _lastIconKey;
    private CancellationTokenSource? _celebrationCts;
    private bool _celebrating;
    private bool _hasLabelInMenu;

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
        RebuildContextMenu(_engine.GetSnapshot());

        ApplySnapshot(_engine.GetSnapshot());
        _engine.SnapshotChanged += ApplySnapshot;
    }

    public void RefreshContextMenuLabels()
    {
        RebuildContextMenu(_engine.GetSnapshot());
    }

    /// <summary>
    /// Plays a brief 5-frame (~1.25s) celebration burst in the Dock icon on phase completion.
    /// </summary>
    public void PlayCelebration(PomodoroPhase completedPhase)
    {
        CancellationTokenSource cts;
        lock (_animLock)
        {
            _celebrationCts?.Cancel();
            _celebrationCts?.Dispose();
            cts = new CancellationTokenSource();
            _celebrationCts = cts;
            _celebrating = true;
        }

        _ = Task.Run(async () =>
        {
            const int totalFrames = 5;
            try
            {
                for (var frame = 0; frame < totalFrames; frame++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    Icon = _iconRenderer.CreateCelebrationIcon(completedPhase, frame, totalFrames);
                    await Task.Delay(240, cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                lock (_animLock)
                {
                    if (_celebrationCts == cts)
                    {
                        _celebrating = false;
                        _lastIconKey = null;
                    }
                }

                ApplySnapshot(_engine.GetSnapshot());
            }
        });
    }

    private void RebuildContextMenu(PomodoroSnapshot snapshot)
    {
        var cfg = _engine.Config;
        var hasLabel = !string.IsNullOrWhiteSpace(snapshot.FocusLabel);
        _hasLabelInMenu = hasLabel;

        var items = new List<IContextItem>
        {
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
                "com.pomodorocmdpal.longbreak"))
        };

        if (hasLabel)
        {
            items.Add(new CommandContextItem(new PomodoroActionCommand(
                "Clear Focus Label",
                new IconInfo("\uE894"),
                () => _engine.ClearFocusLabel(),
                "com.pomodorocmdpal.clearlabel")));
        }

        items.Add(new CommandContextItem(new PomodoroActionCommand(
            "Reset All Cycles",
            new IconInfo("\uE777"),
            () => _engine.ResetAll(),
            "com.pomodorocmdpal.resetall")));

        items.Add(new CommandContextItem(_page)
        {
            Title = "Open Pomodoro Controls",
            Icon = new IconInfo("\uE713")
        });

        MoreCommands = [.. items];
    }

    private void ApplySnapshot(PomodoroSnapshot snapshot)
    {
        Title = snapshot.FormattedTime;
        Subtitle = snapshot.CompactSubtitle;
        _toggleCommand.Name = snapshot.State == PomodoroState.Running
            ? "Pause Pomodoro"
            : "Start Pomodoro";

        var hasLabel = !string.IsNullOrWhiteSpace(snapshot.FocusLabel);
        if (hasLabel != _hasLabelInMenu)
        {
            RebuildContextMenu(snapshot);
        }

        lock (_animLock)
        {
            if (_celebrating)
            {
                return;
            }
        }

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
        lock (_animLock)
        {
            _celebrationCts?.Cancel();
            _celebrationCts?.Dispose();
            _celebrationCts = null;
        }
    }
}
