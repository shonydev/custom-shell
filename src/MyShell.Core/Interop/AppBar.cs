using System.Windows;
using System.Windows.Interop;

namespace MyShell.Core.Interop;

public enum ScreenEdge
{
    Left,
    Top,
    Right,
    Bottom
}

public sealed class AppBar(Window window, uint callbackMessageId) : IDisposable
{
    private readonly HwndSource _source = (HwndSource)PresentationSource.FromVisual(window)!;
    private bool _registered;

    /// <summary>Value the AppBar callback message carries when something
    /// changed that might affect our docked position (another AppBar
    /// docked/undocked, display reconfigured). Compare against
    /// <c>wParam</c> in your window's message hook.</summary>
    public const uint PosChanged = NativeMethods.ABN_POSCHANGED;

    /// <summary>Registers (or looks up) a system-wide window message id for
    /// <paramref name="name"/> - needed so Explorer has something to send
    /// us AppBar notifications on. Wraps NativeMethods since it's internal
    /// to this assembly and the Host project only needs this one call.</summary>
    public static uint RegisterCallbackMessage(string name)
    {
        var id = NativeMethods.RegisterWindowMessage(name);
        return id == 0 ? 0x8000u /* WM_APP fallback, extremely unlikely path */ : id;
    }

    public void DockTo(ScreenEdge edge, double height)
    {
        var handle = _source.Handle;
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = handle,
            uCallbackMessage = callbackMessageId,
            uEdge = (uint)edge,
        };

        if (!_registered)
        {
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
            _registered = true;
        }

        var workArea = SystemParameters.WorkArea;
        data.rc.left = (int)workArea.Left;
        data.rc.top = (int)workArea.Top;
        data.rc.right = (int)workArea.Right;
        data.rc.bottom = (int)(workArea.Top + height);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref data);
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref data);
        window.Left = data.rc.left;
        window.Top = data.rc.top;
        window.Width = data.rc.right - data.rc.left;
        window.Height = data.rc.bottom - data.rc.top;
    }

    public void Dispose()
    {
        if (!_registered)
            return;

        var data = new NativeMethods.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = _source.Handle,
        };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
        _registered = false;
    }
}
