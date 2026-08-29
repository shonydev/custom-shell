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
