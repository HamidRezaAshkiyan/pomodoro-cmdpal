using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PomodoroCmdPal.Core;

namespace PomodoroCmdPal.Services;

internal sealed class NotificationService : IDisposable
{
    private const string ActionKey = "action";
    private const string ActionStartNext = "start_next";
    private const string ActionBonusFocus5 = "bonus_focus_5";

    private readonly Action _onStartNext;
    private readonly Action _onBonusFocusFiveMinutes;
    private bool _initialized;

    public NotificationService(Action onStartNext, Action onBonusFocusFiveMinutes)
    {
        _onStartNext = onStartNext;
        _onBonusFocusFiveMinutes = onBonusFocusFiveMinutes;

        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _initialized = true;
        }
        catch
        {
            // Gracefully degrade if running unpackaged or notifications are disabled.
            _initialized = false;
        }
    }

    public void NotifyPhaseCompleted(
        PomodoroPhase completedPhase,
        PomodoroPhase nextPhase,
        PomodoroSnapshot snapshot,
        bool enableSound)
    {
        if (!_initialized)
        {
            return;
        }

        var labelPrefix = !string.IsNullOrWhiteSpace(snapshot.FocusLabel)
            ? $"\"{snapshot.FocusLabel}\" complete! "
            : string.Empty;

        var (title, body, startButtonLabel) = (completedPhase, nextPhase) switch
        {
            (PomodoroPhase.Focus, PomodoroPhase.LongBreak) => (
                "Focus Complete — Long Break!",
                $"{labelPrefix}All {snapshot.CyclesBeforeLongBreak} focus cycles done ({snapshot.TodayStats.FormattedSummary}). Take a {snapshot.FormattedTime} long break.",
                $"Start Long Break ({snapshot.FormattedTime})"),
            (PomodoroPhase.Focus, _) => (
                $"Focus Complete ({snapshot.CompletedFocusCycles}/{snapshot.CyclesBeforeLongBreak})",
                $"{labelPrefix}{snapshot.TodayStats.FormattedSummary}. Time for a {snapshot.FormattedTime} short break.",
                $"Start Break ({snapshot.FormattedTime})"),
            (PomodoroPhase.LongBreak, _) => (
                "Long Break Finished",
                $"Ready for a fresh Pomodoro cycle ({snapshot.FormattedTime} Focus).",
                $"Start Focus ({snapshot.FormattedTime})"),
            _ => (
                "Break Finished",
                $"Ready for Focus cycle {snapshot.CurrentCycleNumber}/{snapshot.CyclesBeforeLongBreak} ({snapshot.FormattedTime}).",
                $"Start Focus ({snapshot.FormattedTime})")
        };

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body);

            if (enableSound)
            {
                builder.SetAudioEvent(AppNotificationSoundEvent.Reminder);
            }
            else
            {
                builder.MuteAudio();
            }

            if (snapshot.State != PomodoroState.Running)
            {
                builder.AddButton(new AppNotificationButton(startButtonLabel)
                    .AddArgument(ActionKey, ActionStartNext));
            }

            if (completedPhase == PomodoroPhase.Focus)
            {
                builder.AddButton(new AppNotificationButton("+5 min Focus")
                    .AddArgument(ActionKey, ActionBonusFocus5));
            }

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch
        {
            // Ignore transient notification delivery errors.
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue(ActionKey, out var action))
        {
            return;
        }

        switch (action)
        {
            case ActionStartNext:
                _onStartNext();
                break;
            case ActionBonusFocus5:
                _onBonusFocusFiveMinutes();
                break;
        }
    }

    public void Dispose()
    {
        if (_initialized)
        {
            try
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
            }
            catch
            {
                // Ignore shutdown errors.
            }

            _initialized = false;
        }
    }
}
