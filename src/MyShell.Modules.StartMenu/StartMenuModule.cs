using System.IO;
using System.Windows;
using System.Windows.Controls;
using MyShell.Core.Contracts;

namespace MyShell.Modules.StartMenu;

/// <summary>
/// Port your existing Start Menu shortcut enumeration + hidden-apps list
/// here. Kept separate from Taskbar/Tray because it will likely grow its
/// own window (a launcher overlay), not just a bar widget - a module can
/// own more than "one row of UI" when it needs to.
/// </summary>
public sealed class StartMenuModule : IShellModule
{
    public string Name => "start-menu";

    private static readonly string[] ShortcutFolders =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
    };

    public void Initialize(IShellContext context) { }

    public IEnumerable<IShellWidget> CreateWidgets()
    {
        yield return new SearchButtonWidget();
    }

    public void Shutdown() { }

    /// <summary>Basic recursive .lnk enumeration - port your hidden-apps
    /// filtering (stored in the user data folder in the old project) here.</summary>
    public static IEnumerable<string> EnumerateShortcuts()
    {
        foreach (var folder in ShortcutFolders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                yield return Path.GetFileNameWithoutExtension(file);
        }
    }
}

internal sealed class SearchButtonWidget : IShellWidget
{
    public string Id => "start-menu.search-button";
    public WidgetDock PreferredDock => WidgetDock.Left;
    public int Order => -100; // always first

    public FrameworkElement CreateView()
    {
        var icon = new TextBlock
        {
            Text = "\uE721", // Segoe Fluent Icons: Zoom (magnifying glass)
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "BarIconFontFamily");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "BarAccentBrush");

        var label = new TextBlock
        {
            Text = "Search",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
        };
        label.SetResourceReference(TextBlock.FontFamilyProperty, "BarFontFamily");
        label.SetResourceReference(TextBlock.ForegroundProperty, "BarForegroundBrush");

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(icon);
        content.Children.Add(label);

        var button = new Button
        {
            Content = content,
            Margin = new Thickness(10, 0, 10, 0),
            Padding = new Thickness(0),
        };
        button.SetResourceReference(Button.StyleProperty, "BarTextButtonStyle");
        // TODO: open the actual search/launcher surface on click - this is
        // still just the bar chrome, StartMenuModule.EnumerateShortcuts()
        // is what a real launcher window would consume.
        return button;
    }
}
