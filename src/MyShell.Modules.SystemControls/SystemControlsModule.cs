using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
/// port than this bar restyle). Battery, Volume and Brightness are fully
/// wired up: Battery via GetSystemPowerStatus, Volume via a small Core
/// Audio COM wrapper (see MyShell.Core.Interop.SystemVolume) instead of
/// NAudio, and Brightness via WMI's WmiMonitorBrightness(Methods) instead
/// of System.Management-wrapped WinRT.
/// </summary>
public sealed class SystemControlsModule : IShellModule
{
    public string Name => "system-controls";

    public void Initialize(IShellContext context)
    {
    }

    public IEnumerable<IShellWidget> CreateWidgets()
    {
        yield return new VolumeWidget();
        yield return new BrightnessWidget();
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

/// <summary>
/// Reads/writes the default playback device's volume via
/// <see cref="MyShell.Core.Interop.SystemVolume"/> (Core Audio COM, no
/// NAudio dependency). Click toggles mute; scrolling over the icon nudges
/// volume by 2%, matching how Windows' own volume icon behaves.
/// </summary>
internal sealed class VolumeWidget : IShellWidget
{
    public string Id => "system-controls.volume";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => -20;

    public FrameworkElement CreateView()
    {
        var volume = new SystemVolume();

        var icon = TrayIconStyle.Glyph("\uE995", fontSize: 14);
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

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(icon);
        panel.Children.Add(label);

        var button = new Button { Content = panel, Padding = new Thickness(2, 0, 2, 0) };
        button.SetResourceReference(Button.StyleProperty, "BarTextButtonStyle");

        if (!volume.IsAvailable)
        {
            // No default playback device (e.g. no audio hardware) - show a
            // disabled glyph instead of a widget that silently does nothing.
            icon.Text = "\uE74F"; // Segoe Fluent Icons: Mute
            label.Text = "--";
            button.IsEnabled = false;
            button.Unloaded += (_, _) => volume.Dispose();
            return button;
        }

        void Refresh()
        {
            var muted = volume.IsMuted();
            var pct = (int)Math.Round(volume.GetVolume() * 100);
            icon.Text = VolumeGlyph(pct, muted);
            label.Text = muted ? "Muted" : $"{pct}%";
        }

        var popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Focusable = true,
        };

        var flyout = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Width = 220,
        };

        var flyoutPanel = new StackPanel { Orientation = Orientation.Vertical };

        var header = new TextBlock
        {
            Text = "Volume",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Round(volume.GetVolume() * 100),
            Width = 170,
            Margin = new Thickness(0, 0, 0, 6),
            Foreground = Brushes.White,
            IsMoveToPointEnabled = true,
        };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var downButton = new Button { Content = "-5%", Width = 54, Margin = new Thickness(0, 0, 8, 0) };
        var muteButton = new Button { Content = "Mute", Width = 58, Margin = new Thickness(0, 0, 8, 0) };
        var upButton = new Button { Content = "+5%", Width = 54 };
        controls.Children.Add(downButton);
        controls.Children.Add(muteButton);
        controls.Children.Add(upButton);

        flyoutPanel.Children.Add(header);
        flyoutPanel.Children.Add(slider);
        flyoutPanel.Children.Add(controls);
        flyout.Child = flyoutPanel;
        popup.Child = flyout;

        void SyncSliderFromVolume()
        {
            var pct = Math.Clamp((int)Math.Round(volume.GetVolume() * 100), 0, 100);
            slider.Value = pct;
            Refresh();
        }

        slider.ValueChanged += (_, _) =>
        {
            var value = (float)(slider.Value / 100.0);
            volume.SetVolume(Math.Clamp(value, 0f, 1f));
            if (volume.IsMuted() && slider.Value > 0)
                volume.SetMuted(false);
            Refresh();
        };

        downButton.Click += (_, _) =>
        {
            var next = Math.Clamp(volume.GetVolume() - 0.05f, 0f, 1f);
            volume.SetVolume(next);
            if (volume.IsMuted() && next > 0)
                volume.SetMuted(false);
            SyncSliderFromVolume();
        };

        upButton.Click += (_, _) =>
        {
            var next = Math.Clamp(volume.GetVolume() + 0.05f, 0f, 1f);
            volume.SetVolume(next);
            if (volume.IsMuted() && next > 0)
                volume.SetMuted(false);
            SyncSliderFromVolume();
        };

        muteButton.Click += (_, _) =>
        {
            volume.SetMuted(!volume.IsMuted());
            SyncSliderFromVolume();
        };

        Refresh();

        button.Click += (_, _) =>
        {
            if (!popup.IsOpen)
            {
                slider.Value = Math.Clamp(Math.Round(volume.GetVolume() * 100), 0, 100);
                popup.IsOpen = true;
            }
            else
            {
                popup.IsOpen = false;
            }
        };

        // Scroll-to-adjust, same gesture Windows' own volume icon supports.
        button.PreviewMouseWheel += (_, e) =>
        {
            var delta = e.Delta > 0 ? 0.02f : -0.02f;
            volume.SetVolume(Math.Clamp(volume.GetVolume() + delta, 0f, 1f));
            if (volume.IsMuted() && delta > 0)
                volume.SetMuted(false);
            SyncSliderFromVolume();
            e.Handled = true;
        };

        button.Unloaded += (_, _) =>
        {
            popup.IsOpen = false;
            volume.Dispose();
        };
        return button;
    }

    /// <summary>Segoe Fluent Icons volume glyphs: Mute, Volume0 (0%),
    /// Volume1 (low), Volume2 (mid), Volume3 (full).</summary>
    private static string VolumeGlyph(int pct, bool muted)
    {
        if (muted || pct == 0) return "\uE74F";
        if (pct < 33) return "\uE993";
        if (pct < 66) return "\uE994";
        return "\uE995";
    }
}

/// <summary>
/// Reads/writes the primary display's brightness via WMI's
/// <c>WmiMonitorBrightness</c> (read) and
/// <c>WmiMonitorBrightnessMethods.WmiSetBrightness</c> (write) under the
/// <c>root\WMI</c> namespace. This only works for internal laptop panels
/// exposing WMI brightness control (most external desktop monitors don't) -
/// see the class-level TODO note if you need DDC/CI control for external
/// displays instead.
/// </summary>
internal sealed class BrightnessWidget : IShellWidget
{
    public string Id => "system-controls.brightness";
    public WidgetDock PreferredDock => WidgetDock.Right;
    public int Order => -10;

    public FrameworkElement CreateView()
    {
        var icon = TrayIconStyle.Glyph("\uE706", fontSize: 14); // Segoe Fluent Icons: Brightness
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

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(icon);
        panel.Children.Add(label);

        if (!WmiBrightness.TryGetCurrent(out var initial))
        {
            // No WMI brightness reporter (desktop monitor, or a laptop
            // whose vendor uses DDC/CI or a proprietary driver instead of
            // exposing WmiMonitorBrightness) - hide rather than fake a value.
            panel.Visibility = Visibility.Collapsed;
            return panel;
        }

        label.Text = $"{initial}%";

        panel.PreviewMouseWheel += (_, e) =>
        {
            if (!WmiBrightness.TryGetCurrent(out var current))
                return;

            var next = Math.Clamp(current + (e.Delta > 0 ? 5 : -5), 0, 100);
            if (WmiBrightness.TrySet(next))
                label.Text = $"{next}%";

            e.Handled = true;
        };

        return panel;
    }
}

/// <summary>Thin WMI wrapper kept private to this file - BrightnessWidget
/// is the only consumer, so there's no need for a Core-level façade like
/// SystemVolume gets.</summary>
internal static class WmiBrightness
{
    public static bool TryGetCurrent(out int percent)
    {
        percent = 0;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var o in searcher.Get())
            {
                percent = Convert.ToInt32(o["CurrentBrightness"]);
                return true;
            }
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException)
        {
            // No WMI brightness class present, or no permission to query
            // it - both mean "treat as unavailable", not a crash.
        }

        return false;
    }

    public static bool TrySet(int percent)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (var o in searcher.Get())
            {
                var managementObject = (System.Management.ManagementObject)o;
                // Params: (Timeout in seconds, target brightness 0-100).
                managementObject.InvokeMethod("WmiSetBrightness", [0u, (byte)percent]);
                return true;
            }
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException)
        {
        }

        return false;
    }
}
