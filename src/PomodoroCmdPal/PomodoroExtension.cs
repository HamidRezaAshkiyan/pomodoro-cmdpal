using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace PomodoroCmdPal;

[Guid("7f4c8b2e-9a3d-4e61-b5c2-1d8a0f93e4a1")]
public sealed partial class PomodoroExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly PomodoroCommandsProvider _provider = new();

    public PomodoroExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType) => providerType switch
    {
        ProviderType.Commands => _provider,
        _ => null
    };

    public void Dispose()
    {
        _provider.Dispose();
        _extensionDisposedEvent.Set();
    }
}
