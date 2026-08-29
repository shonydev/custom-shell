namespace MyShell.Core.Interop;

/// <summary>
/// Public façade over <see cref="CoreAudioInterop"/> (which is internal to
/// this assembly) - holds the COM endpoint open for the lifetime of the
/// instance instead of re-activating it on every volume read, since the
/// VolumeWidget polls/updates this fairly often (scroll-to-adjust).
/// Dispose when the owning widget unloads.
/// </summary>
public sealed class SystemVolume : IDisposable
{
    private readonly IDisposable? _endpoint;
    private readonly Action<float>? _setVolume;
    private readonly Func<float>? _getVolume;
    private readonly Action<bool>? _setMute;
    private readonly Func<bool>? _getMute;

    public SystemVolume()
    {
        _endpoint = CoreAudioInterop.TryGetDefaultEndpointVolume(
            out _setVolume, out _getVolume, out _setMute, out _getMute);
    }

    /// <summary>False when there's no default playback device (or the COM
    /// activation failed for any other reason) - the widget should hide or
    /// show a disabled state rather than pretend a volume exists.</summary>
    public bool IsAvailable => _endpoint is not null;

    /// <summary>0.0 - 1.0. Returns 0 if <see cref="IsAvailable"/> is false.</summary>
    public float GetVolume() => _getVolume?.Invoke() ?? 0f;

    public void SetVolume(float level) => _setVolume?.Invoke(level);

    public bool IsMuted() => _getMute?.Invoke() ?? false;

    public void SetMuted(bool muted) => _setMute?.Invoke(muted);

    public void Dispose() => _endpoint?.Dispose();
}
