using Microsoft.Win32;

namespace PomodoroCmdPal.Services;

internal sealed class SessionLockMonitor : IDisposable
{
    private readonly Action _onSessionLocked;
    private bool _subscribed;

    public SessionLockMonitor(Action onSessionLocked)
    {
        _onSessionLocked = onSessionLocked;
        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _subscribed = true;
        }
        catch
        {
            _subscribed = false;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            _onSessionLocked();
        }
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
            catch
            {
                // Ignore shutdown errors.
            }

            _subscribed = false;
        }
    }
}
