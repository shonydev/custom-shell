using System.Windows;
using System.Windows.Threading;
using MyShell.Core.Contracts;
using MyShell.Core.Messaging;
using MyShell.Core.Modules;
using MyShell.Modules.Clock;
using MyShell.Modules.StartMenu;
using MyShell.Modules.SystemControls;
using MyShell.Modules.Tray;

namespace MyShell.Host;

/// <summary>
/// Entry point / composition root. Reads the mode flag, wires the
/// EventBus, registers modules into a single <see cref="ModuleRegistry"/>,
/// starts <see cref="ShellModeManager"/> (which handles hiding/restoring
/// the real taskbar in DevOverlay) and creates the bar window.
///
/// <see cref="TaskbarModule"/> is intentionally left out of the
/// registration below: the current reference design only shows the date
/// centered on the bar, with no open-window list. To bring it back, add
/// <c>.Add(new TaskbarModule())</c> to the chain and give it an
/// <see cref="WidgetDock"/>/<see cref="IShellWidget.Order"/> that doesn't
/// collide with <see cref="ClockModule"/>'s centered date widget.
/// </summary>
public partial class App : Application, IShellContext
{
    private ModuleRegistry? _registry;
    private ShellModeManager? _modeManager;

    public ShellMode Mode { get; private set; } = ShellMode.DevOverlay;
    public IEventBus EventBus { get; private set; } = null!;
    public IServiceProvider Services => throw new NotSupportedException(
        "No DI container yet - see README 'DI container' row. Modules take what they need through their own constructors for now.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Mode = e.Args.Contains("--full-shell") ? ShellMode.FullShell : ShellMode.DevOverlay;
        EventBus = new EventBus(Dispatcher.CurrentDispatcher);

        // Catch anything that slips past a widget's own try/catch. In
        // DevOverlay this just logs and keeps the overlay alive; in
        // FullShell letting an unhandled exception kill this process means
        // the session ends, so this is the last line of defense, not a
        // cosmetic log statement.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            System.Diagnostics.Debug.WriteLine($"[App] fatal unhandled exception: {args.ExceptionObject}");

        _modeManager = new ShellModeManager(Mode, EventBus);
        _modeManager.OnStartup(this);

        _registry = new ModuleRegistry()
            .Add(new StartMenuModule())
            .Add(new ClockModule())
            .Add(new SystemControlsModule())
            .Add(new TrayModule());
        // .Add(new TaskbarModule()) <- see class doc comment above.

        _registry.InitializeAll(this);

        var window = new ShellWindow(Mode, _registry);
        window.Show();

        Exit += (_, _) => _registry.ShutdownAll();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[App] dispatcher exception: {e.Exception}");

        // In DevOverlay this process dying is harmless (explorer's taskbar
        // gets restored by ShellModeManager's Exit handler), so let it
        // crash loudly instead of limping along in a half-broken state.
        // In FullShell, crashing here ends the session - that's the whole
        // reason FullShell needs a VM with a snapshot before you try it.
        e.Handled = false;
    }
}
