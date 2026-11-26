using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PomodoroCmdPal.Core;
using PomodoroCmdPal.Dock;
using PomodoroCmdPal.Pages;
using PomodoroCmdPal.Services;

namespace PomodoroCmdPal;

public sealed partial class PomodoroCommandsProvider : CommandProvider
{
    private readonly PomodoroSettingsManager _settingsManager;
    private readonly PomodoroEngine _engine;
    private readonly NotificationService _notifications;
    private readonly SessionLockMonitor _lockMonitor;
    private readonly PomodoroPage _page;
    private readonly PomodoroDockBandItem _dockBand;
    private readonly ICommandItem[] _topLevelCommands;

    public PomodoroCommandsProvider()
    {
        Id = "com.pomodorocmdpal.extension";
        DisplayName = "Pomodoro Timer";
        Icon = new IconInfo("\uE916");

        _settingsManager = new PomodoroSettingsManager();
        Settings = _settingsManager.Settings;

        var statsPath = Path.Combine(PomodoroSettingsManager.GetDataDirectory(), "stats.json");
        var statsStore = new JsonFileStatsStore(statsPath);

        _engine = new PomodoroEngine(_settingsManager.BuildConfig(), statsStore: statsStore);
        _notifications = new NotificationService(
            onStartNext: () => _engine.Start(),
            onBonusFocusFiveMinutes: () => _engine.StartBonusFocus(TimeSpan.FromMinutes(5)));
        _lockMonitor = new SessionLockMonitor(() => _engine.HandleSessionLocked());

        _engine.PhaseCompleted += OnPhaseCompleted;
        _settingsManager.ConfigChanged += OnConfigChanged;

        _page = new PomodoroPage(_engine, preset => _settingsManager.ApplyPreset(preset));
        _dockBand = new PomodoroDockBandItem(_engine, _page);

        _topLevelCommands =
        [
            new CommandItem(_page)
            {
                Title = "Pomodoro Timer",
                Subtitle = "Minimal Pomodoro timer with live Dock progress ring, focus labels, presets & streaks",
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

    private void OnConfigChanged(PomodoroConfig config)
    {
        _engine.UpdateConfig(config, applyToCurrentIfIdle: true);
        _dockBand.RefreshContextMenuLabels();
    }

    private void OnPhaseCompleted(PomodoroPhase completedPhase, PomodoroPhase nextPhase)
    {
        _dockBand.PlayCelebration(completedPhase);
        _notifications.NotifyPhaseCompleted(
            completedPhase,
            nextPhase,
            _engine.GetSnapshot(),
            _engine.Config.EnableSound);
    }

    public override void Dispose()
    {
        _settingsManager.ConfigChanged -= OnConfigChanged;
        _engine.PhaseCompleted -= OnPhaseCompleted;
        _lockMonitor.Dispose();
        _dockBand.Dispose();
        _page.Dispose();
        _notifications.Dispose();
        _engine.Dispose();
        base.Dispose();
    }
}
