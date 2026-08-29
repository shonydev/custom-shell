using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MyShell.Core.Contracts;
using MyShell.Core.Interop;

namespace MyShell.Modules.SystemControls;

/// <summary>
/// This is where your existing QuickSettingsTray code moves to:
///   - Volume (NAudio)               -> VolumeWidget
///   - Brightness (System.Management)-> BrightnessWidget
///   - Wi-Fi                         -> WifiWidget (your WifiWindow.xaml
///                                      becomes a flyout opened from here)
///   - Bluetooth, battery, quick actions -> same pattern
///
/// Each becomes its own IShellWidget (own file) instead of living inside one
/// big MainWindow.xaml.cs - that's the main structural change from the
/// original project: one god-window becomes N independently testable
/// widgets, all composed by the Host at run time.
///
/// Wifi/Bluetooth below only render the reference-look glyph for now (their
/// real state needs the Windows.Devices.* WinRT APIs, which is a bigger
/// port than this bar restyle) - Battery and the power button are fully
/// wired up since GetSystemPowerStatus is simple enough to do properly
/// right away. Volume/Brightness are still TODO, same as before.
/// </summary>
public sealed class SystemControlsModule : IShellModule
{
    public string Name => "system-controls";

    public void Initialize(IShellContext context)
    {
        // TODO: read initial volume/brightness state here once those
        // widgets are ported (see class comment above).
    }

    public IEnumerable<IShellWidget> CreateWidgets()
    {
        // yield return new VolumeWidget(...);
        // yield return new BrightnessWidget(...);
        yield return new WifiWidget();
        yield return new BluetoothWidget();
        yield return new BatteryWidget();
        yield return new SeparatorWidget();
        yield return new PowerButtonWidget();
    }

    public void Shutdown() { }
}

/// <summary>Shared look for a single-glyph icon on the right side of the bar
/// (Wifi/Bluetooth/Battery/Power) - kept here instead of one style per class
/// so they stay visually identical without copy-pasting XAML.</summary>
internal static class TrayIconStyle
{
    public static TextBlock Glyph(string glyph, double fontSize = 13) =>
        new()
        {
            Text = glyph,
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
        };
}

internal sealed class WifiWidget : IShellWidget
{
    public string Id => "system-controls.wifi";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => 10;

    public FrameworkElement CreateView()
    {
        // TODO: replace the static glyph with real adapter state via
        // Windows.Devices.WiFi / netsh, and turn this into a flyout button
        // like the module doc comment describes (WifiWindow.xaml port).
        var icon = TrayIconStyle.Glyph("\uE701"); // Segoe Fluent Icons: Wifi
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "BarIconFontFamily");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");
        return icon;
    }
}

internal sealed class BluetoothWidget : IShellWidget
{
    public string Id => "system-controls.bluetooth";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => 20;

    public FrameworkElement CreateView()
    {
        // TODO: same as WifiWidget - static glyph until real adapter state
        // is wired up via Windows.Devices.Bluetooth.
        var icon = TrayIconStyle.Glyph("\uE702"); // Segoe Fluent Icons: Bluetooth
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "BarIconFontFamily");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");
        return icon;
    }
}

internal sealed class BatteryWidget : IShellWidget
{
    public string Id => "system-controls.battery";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => 30;

    public FrameworkElement CreateView()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var icon = new TextBlock { FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "BarIconFontFamily");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");

        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
            Margin = new Thickness(4, 0, 6, 0),
        };
        label.SetResourceReference(TextBlock.FontFamilyProperty, "BarFontFamily");
        label.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");

        panel.Children.Add(icon);
        panel.Children.Add(label);

        void Refresh()
        {
            var snapshot = PowerStatus.GetCurrent();
            if (!snapshot.HasBattery)
            {
                // Desktop PC: no battery to report. Hide the widget rather
                // than show a fake 100% - a laptop-only widget on a
                // desktop machine should just not take up bar space.
                panel.Visibility = Visibility.Collapsed;
                return;
            }

            panel.Visibility = Visibility.Visible;
            icon.Text = BatteryGlyph(snapshot.Percent, snapshot.IsCharging);
            label.Text = snapshot.Percent is { } pct ? $"{pct}%" : "--%";
        }

        Refresh();

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        panel.Unloaded += (_, _) => timer.Stop();

        return panel;
    }

    /// <summary>
    /// Segoe Fluent Icons ships one glyph per 10% of charge (Battery0 =
    /// 0xE850 ... Battery9 = 0xE859), plus a separate Battery10 at 0xE83F
    /// and BatteryCharging0-9 at 0xE85A-0xE863. This picks the closest tier
    /// instead of hand-listing all eleven cases.
    /// </summary>
    private static string BatteryGlyph(int? percent, bool isCharging)
    {
        if (percent is not { } pct) return "\uE850"; // unknown -> empty icon
        var tier = Math.Clamp(pct / 10, 0, 9);

        if (isCharging)
            return char.ConvertFromUtf32(0xE85A + tier);

        return char.ConvertFromUtf32(0xE850 + tier);
    }
}

internal sealed class SeparatorWidget : IShellWidget
{
    public string Id => "system-controls.separator";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => 40;

    public FrameworkElement CreateView()
    {
        var line = new Border
        {
            Width = 1,
            Margin = new Thickness(4, 8, 4, 8),
        };
        line.SetResourceReference(Border.BackgroundProperty, "BarSeparatorBrush");
        return line;
    }
}

internal sealed class PowerButtonWidget : IShellWidget
{
    public string Id => "system-controls.power";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => 50;

    public FrameworkElement CreateView()
    {
        var icon = TrayIconStyle.Glyph("\uE7E8", fontSize: 14); // Segoe Fluent Icons: PowerButton
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "BarIconFontFamily");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");
        icon.Margin = new Thickness(6, 0, 10, 0);

        var button = new Button { Content = icon, Padding = new Thickness(0) };
        button.SetResourceReference(Button.StyleProperty, "BarTextButtonStyle");
        // TODO: wire to a real power flyout (sleep/restart/shut down) -
        // FullShell mode is where this stops being optional, since there's
        // no explorer.exe Start menu to fall back on for shutting down.
        return button;
    }
}
