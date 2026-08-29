using MyShell.Core.Contracts;

namespace MyShell.Core.Modules;

public sealed class ModuleRegistry
{
    private readonly List<IShellModule> _modules = [];

    public ModuleRegistry Add(IShellModule module)
    {
        _modules.Add(module);
        return this;
    }

    public void InitializeAll(IShellContext context)
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Initialize(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleRegistry] module '{module.Name}' failed to initialize: {ex}");
            }
        }
    }

    public IEnumerable<(IShellModule Module, IShellWidget Widget)> CollectWidgets()
    {
        foreach (var module in _modules)
        {
            IEnumerable<IShellWidget> widgets;
            try
            {
                widgets = module.CreateWidgets();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleRegistry] module '{module.Name}' failed to create widgets: {ex}");
                continue;
            }

            foreach (var widget in widgets)
                yield return (module, widget);
        }
    }

    public void ShutdownAll()
    {
        foreach (var module in _modules.AsEnumerable().Reverse())
        {
            try
            {
                module.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleRegistry] module '{module.Name}' failed to shut down: {ex}");
            }
        }
    }
}
