using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace PomodoroCmdPal;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            ComServer server = new();
            try
            {
                using ManualResetEvent extensionDisposedEvent = new(false);
                using PomodoroExtension extensionInstance = new(extensionDisposedEvent);

                server.RegisterClass<PomodoroExtension, IExtension>(() => extensionInstance);
                server.Start();

                extensionDisposedEvent.WaitOne();
                server.Stop();
            }
            finally
            {
                server.UnsafeDispose();
            }
        }
    }
}
