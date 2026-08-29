using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MyShell.Core.Contracts;

namespace MyShell.Modules.Clock;

/// <summary>
/// Owns the center-of-the-bar date readout ("Sábado, 29 Agosto"). Kept as
/// its own module rather than folded into StartMenu/SystemControls because
/// it's a distinct feature with its own update loop - the README's rule of
/// thumb ("una feature nueva casi siempre es un módulo nuevo") applies here
/// even though the widget itself is tiny.
/// </summary>
public sealed class ClockModule : IShellModule
{
    public string Name => "clock";

    public void Initialize(IShellContext context) { }

    public IEnumerable<IShellWidget> CreateWidgets()
    {
        yield return new DateWidget();
    }

    public void Shutdown() { }
}

/// <summary>
/// Deliberately hardcodes es-ES for formatting instead of using
/// CultureInfo.CurrentCulture: the reference design shows an English
/// "Search" box next to a Spanish date, i.e. the date format is a fixed
/// choice for this bar, not a reflection of the OS display language. Swap
/// to CurrentCulture here if you'd rather it follow Windows' language
/// setting.
/// </summary>
internal sealed class DateWidget : IShellWidget
{
    private static readonly CultureInfo DateCulture = CultureInfo.GetCultureInfo("es-ES");

    public string Id => "clock.date";
    public WidgetDock PreferredDock => WidgetDock.Center;
    public int Order => 0;

    public FrameworkElement CreateView()
    {
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");
        text.SetResourceReference(TextBlock.FontFamilyProperty, "BarFontFamily");

        void Refresh() => text.Text = FormatToday();
        Refresh();

        // A date only needs to be re-checked around midnight, but a cheap
        // once-a-minute tick keeps this correct without any extra state -
        // no need to compute "time until next midnight" for a bar label.
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        timer.Tick += (_, _) => Refresh();
        timer.Start();

        text.Unloaded += (_, _) => timer.Stop();

        return text;
    }

    private static string FormatToday()
    {
        var now = DateTime.Now;
        var dayName = DateCulture.TextInfo.ToTitleCase(now.ToString("dddd", DateCulture));
        var monthName = DateCulture.TextInfo.ToTitleCase(now.ToString("MMMM", DateCulture));
        return $"{dayName}, {now.Day} {monthName}";
    }
}
