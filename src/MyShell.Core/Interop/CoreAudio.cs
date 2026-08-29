using System.Runtime.InteropServices;

namespace MyShell.Core.Interop;

/// <summary>
/// Minimal Core Audio COM interop - just enough to get the default output
/// endpoint and read/set its master volume + mute state. This replaces
/// what the old project used NAudio for; NAudio is a fine choice too, but
/// pulling in a whole package for three COM calls didn't seem worth it
/// here. If you'd rather have per-app volume, waveform metering, or device
/// switching, NAudio (or CSCore) will get you there faster than extending
/// this file - swap it in and delete this one.
/// </summary>
internal static class CoreAudioInterop
{
    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidIMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IidIAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private const int eRender = 0;
    private const int eMultimedia = 1;

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, nint pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int NotImpl1();
        int NotImpl2();
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float level, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float level);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    private const int CLSCTX_ALL = 23;

    /// <summary>Activates the endpoint volume interface for the current
    /// default playback device. Returns null if no device is available
    /// (e.g. no audio hardware) rather than throwing, since a missing
    /// audio device shouldn't crash the bar.</summary>
    public static IDisposable? TryGetDefaultEndpointVolume(out Action<float>? setVolume,
        out Func<float>? getVolume, out Action<bool>? setMute, out Func<bool>? getMute)
    {
        setVolume = null;
        getVolume = null;
        setMute = null;
        getMute = null;

        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator)
                ?? throw new InvalidOperationException("MMDeviceEnumerator COM class not found.");
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;

            if (enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out var device) != 0)
                return null;

            var iid = IidIAudioEndpointVolume;
            if (device.Activate(ref iid, CLSCTX_ALL, 0, out var raw) != 0)
                return null;

            var endpoint = (IAudioEndpointVolume)raw;
            var context = Guid.Empty;

            setVolume = level => endpoint.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
            getVolume = () => endpoint.GetMasterVolumeLevelScalar(out var level) == 0 ? level : 0f;
            setMute = mute => endpoint.SetMute(mute, ref context);
            getMute = () => endpoint.GetMute(out var mute) == 0 && mute;

            return new ComRelease(endpoint);
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private sealed class ComRelease(object comObject) : IDisposable
    {
        public void Dispose()
        {
            if (Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
        }
    }
}
