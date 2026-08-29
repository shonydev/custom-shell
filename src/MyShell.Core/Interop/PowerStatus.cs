using static MyShell.Core.Interop.NativeMethods;

namespace MyShell.Core.Interop;

/// <summary>
/// Thin wrapper around GetSystemPowerStatus so the SystemControls module
/// doesn't need its own P/Invoke for something this simple. This is a
/// poll-on-demand snapshot, not a subscription - if you want the battery
/// widget to react instantly to plug/unplug events instead of on its own
/// timer tick, that means handling WM_POWERBROADCAST, which is a bigger
/// change (needs a message-only window like TrayHost has) and not needed
/// just to show a percentage in the bar.
/// </summary>
public static class PowerStatus
{
    public readonly record struct Snapshot(bool HasBattery, int? Percent, bool IsCharging);

    public static Snapshot GetCurrent()
    {
        if (!GetSystemPowerStatus(out var status))
            return new Snapshot(HasBattery: false, Percent: null, IsCharging: false);

        // BatteryFlag 128 = "no system battery" (desktop PCs). 255 in
        // BatteryLifePercent means "unknown" - both are documented quirks
        // of this ancient API, not bugs.
        var hasBattery = status.BatteryFlag != 128;
        int? percent = status.BatteryLifePercent == 255 ? null : status.BatteryLifePercent;
        var isCharging = (status.BatteryFlag & 0x08) != 0;

        return new Snapshot(hasBattery, percent, isCharging);
    }
}
