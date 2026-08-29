using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MyShell.Core.Contracts;
using MyShell.Core.Interop;
using MyShell.Core.Modules;

namespace MyShell.Host;

/// <summary>
/// Takes whatever <see cref="ModuleRegistry.CollectWidgets"/> returns,
/// sorts each dock by <see cref="IShellWidget.Order"/> and drops the views
/// into the Left/Center/Right/Tray <see cref="StackPanel"/>s declared in
/// ShellWindow.xaml. Also owns the <see cref="AppBar"/> registration that
/// reserves screen space like a real taskbar would.
/// </summary>
public partial class ShellWindow : Window
{
    private readonly uint _callbackMessageId;
    private readonly double _barHeight;
    private AppBar? _appBar;

    public ShellWindow(ShellMode mode, ModuleRegistry registry)
    {
        InitializeComponent();

        _barHeight = Height; // set in XAML (32px, see appsettings.json note)
        _callbackMessageId = RegisterAppBarCallbackMessage();

        LayoutWidgets(registry);

        SourceInitialized += (_, _) => DockAppBar();
        Closed += (_, _) => _appBar?.Dispose();
    }

    private void LayoutWidgets(ModuleRegistry registry)
    {
        var byDock = new Dictionary<WidgetDock, StackPanel>
        {
            [WidgetDock.Left] = LeftPanel,
            [WidgetDock.Center] = CenterPanel,
            [WidgetDock.Right] = RightPanel,
            [WidgetDock.Tray] = TrayPanel,
        };

        // Group first so ordering is per-dock, not global - a Left widget
        // with Order=100 shouldn't out-rank a Right widget with Order=0,
        // they don't compete for the same slot.
        var grouped = registry.CollectWidgets()
            .GroupBy(pair => pair.Widget.PreferredDock)
            .ToDictionary(g => g.Key, g => g.OrderBy(pair => pair.Widget.Order));

        foreach (var (dock, panel) in byDock)
        {
            if (!grouped.TryGetValue(dock, out var widgets))
                continue;

            foreach (var (module, widget) in widgets)
            {
                try
                {
                    panel.Children.Add(widget.CreateView());
                }
                catch (Exception ex)
                {
                    // A single widget throwing during CreateView() shouldn't
                    // take the rest of the bar down with it.
                    System.Diagnostics.Debug.WriteLine(
                        $"[ShellWindow] widget '{widget.Id}' from module '{module.Name}' failed to render: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// SHAppBarMessage needs a registered window message id so Explorer can
    /// notify us when the work area changes (monitor added/removed,
    /// resolution change, etc). RegisterWindowMessage guarantees a value
    /// that's unique system-wide for this string.
    /// </summary>
    private static uint RegisterAppBarCallbackMessage() =>
        AppBar.RegisterCallbackMessage("MyShell_AppBarCallback");

    private void DockAppBar()
    {
        _appBar = new AppBar(this, _callbackMessageId);
        _appBar.DockTo(ScreenEdge.Top, _barHeight);

        var hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
        hwndSource.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == _callbackMessageId && (uint)wParam == AppBar.PosChanged)
        {
            // Work area changed (e.g. another AppBar docked/undocked, or a
            // display was reconfigured) - re-query our slot so we don't end
            // up overlapping something or floating in the wrong place.
            _appBar?.DockTo(ScreenEdge.Top, _barHeight);
            handled = true;
        }

        return 0;
    }
}
