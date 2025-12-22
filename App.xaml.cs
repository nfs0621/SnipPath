using System.Windows;

namespace SnipPath;

public partial class App : System.Windows.Application
{
    private HotkeyWindow? _hotkeyWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyWindow?.Dispose();
        base.OnExit(e);
    }
}
