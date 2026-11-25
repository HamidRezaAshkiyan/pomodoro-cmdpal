using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PomodoroCmdPal.Commands;

internal sealed partial class PomodoroActionCommand : InvokableCommand
{
    private readonly Action _action;

    public PomodoroActionCommand(string name, IconInfo icon, Action action, string? id = null)
    {
        Name = name;
        Icon = icon;
        _action = action;
        if (!string.IsNullOrWhiteSpace(id))
        {
            Id = id;
        }
    }

    public override ICommandResult Invoke()
    {
        _action();
        return CommandResult.KeepOpen();
    }
}
