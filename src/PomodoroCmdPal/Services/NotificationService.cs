using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PomodoroCmdPal.Core;

namespace PomodoroCmdPal.Services;

internal sealed class NotificationService : IDisposable
{
    private bool _initialized;

    public NotificationService()
    {
        try
        {
            AppNotificationManager.Default.Register();
            _initialized = true;
        }
        catch
        {
            // Gracefully degrade if running unpackaged or notifications are disabled.
            _initialized = false;
        }
    }

    public void NotifyPhaseCompleted(PomodoroPhase completedPhase, PomodoroPhase nextPhase, PomodoroSnapshot snapshot)
    {
        if (!_initialized)
        {
            return;
        }

        var (title, body) = (completedPhase, nextPhase) switch
        {
            (PomodoroPhase.Focus, PomodoroPhase.LongBreak) => (
                "Focus Complete — Long Break!",
                $"All {snapshot.CyclesBeforeLongBreak} focus cycles done. Take a {snapshot.FormattedTime} long break."),
            (PomodoroPhase.Focus, _) => (
                $"Focus Complete ({snapshot.CompletedFocusCycles}/{snapshot.CyclesBeforeLongBreak})",
                $"Nice work! Time for a {snapshot.FormattedTime} short break."),
            (PomodoroPhase.LongBreak, _) => (
                "Long Break Finished",
                "Ready for a fresh Pomodoro cycle (25:00 Focus)."),
            _ => (
                "Break Finished",
                $"Ready for Focus cycle {snapshot.CurrentCycleNumber}/{snapshot.CyclesBeforeLongBreak} ({snapshot.FormattedTime}).")
        };

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .SetAudioEvent(AppNotificationSoundEvent.Reminder)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Ignore transient notification delivery errors.
        }
    }

    public void Dispose()
    {
        if (_initialized)
        {
            try
            {
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
