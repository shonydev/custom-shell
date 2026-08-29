using System.Windows;

namespace MyShell.Core.Contracts;

public enum ShellMode
{
    DevOverlay,
    FullShell
}

public enum WidgetDock
{
    Left,
    Center,
    Right,
    Tray
}

public interface IShellContext
{
    ShellMode Mode { get; }
    Messaging.IEventBus EventBus { get; }
    IServiceProvider Services { get; }
}

public interface IShellModule
{
    string Name { get; }
    void Initialize(IShellContext context);
    IEnumerable<IShellWidget> CreateWidgets();
    void Shutdown();
}

public interface IShellWidget
{
    string Id { get; }
    WidgetDock PreferredDock { get; }
    int Order { get; }
    FrameworkElement CreateView();
}
