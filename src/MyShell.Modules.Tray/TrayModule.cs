using MyShell.Core.Contracts;

namespace MyShell.Modules.Tray;

public sealed class TrayModule : IShellModule
{
    public string Name => "tray";

    public void Initialize(IShellContext context) { }

    public IEnumerable<IShellWidget> CreateWidgets() => [];

    public void Shutdown() { }
}
