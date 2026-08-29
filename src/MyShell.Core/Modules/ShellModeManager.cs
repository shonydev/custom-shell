using System.Windows;
using MyShell.Core.Contracts;
using MyShell.Core.Interop;
using MyShell.Core.Messaging;

namespace MyShell.Core.Modules;

public sealed class ShellModeManager(ShellMode mode, IEventBus eventBus)
{
    public void OnStartup(Application application)
    {
        if (mode == ShellMode.DevOverlay)
            SetExplorerTaskbarVisible(false);

        application.Exit += (_, _) =>
        {
            if (mode == ShellMode.DevOverlay)
                SetExplorerTaskbarVisible(true);
        };
    }

    private static void SetExplorerTaskbarVisible(bool visible)
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar != 0)
            NativeMethods.ShowWindow(taskbar, visible ? NativeMethods.SW_SHOW : NativeMethods.SW_HIDE);
    }
}
