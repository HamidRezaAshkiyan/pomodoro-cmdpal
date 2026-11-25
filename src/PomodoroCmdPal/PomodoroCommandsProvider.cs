using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Core;
using PomodoroCmdPal.Dock;
using PomodoroCmdPal.Pages;
using PomodoroCmdPal.Services;

namespace PomodoroCmdPal;

public sealed partial class PomodoroCommandsProvider : CommandProvider
{
    private readonly PomodoroEngine _engine;
    private readonly NotificationService _notifications;
    private readonly PomodoroPage _page;
    private readonly PomodoroDockBandItem _dockBand;
    private readonly ICommandItem[] _topLevelCommands;

    public PomodoroCommandsProvider()
    {
        Id = "com.pomodorocmdpal.extension";
        DisplayName = "Pomodoro Timer";
        Icon = new IconInfo("\uE916");

        _engine = new PomodoroEngine(new PomodoroConfig());
        _notifications = new NotificationService();
        _engine.PhaseCompleted += OnPhaseCompleted;

        _page = new PomodoroPage(_engine);
        _dockBand = new PomodoroDockBandItem(_engine, _page);

        _topLevelCommands =
        [
            new CommandItem(_page)
            {
                Title = "Pomodoro Timer",
                Subtitle = "Minimal Pomodoro timer (25/5/15) for the Command Palette Dock",
                Icon = new IconInfo("\uE916")
            },
            _dockBand
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _topLevelCommands;

    public override ICommandItem[]? GetDockBands() => [_dockBand];

    public override ICommandItem? GetCommandItem(string id) => id switch
    {
        "com.pomodorocmdpal.toggle" => _dockBand,
        "com.pomodorocmdpal.page" => _topLevelCommands[0],
        _ => null
    };

    private void OnPhaseCompleted(PomodoroPhase completedPhase, PomodoroPhase nextPhase)
    {
        _notifications.NotifyPhaseCompleted(completedPhase, nextPhase, _engine.GetSnapshot());
    }

    public override void Dispose()
    {
        _engine.PhaseCompleted -= OnPhaseCompleted;
        _dockBand.Dispose();
        _page.Dispose();
        _notifications.Dispose();
        _engine.Dispose();
        base.Dispose();
    }
}
