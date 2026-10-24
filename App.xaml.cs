using System.Threading;
using System.Windows;

namespace SnipPath;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\SnipPath.SingleInstance";

    private HotkeyWindow? _hotkeyWindow;
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A second copy can't register the hotkeys, so exit quietly instead of showing errors.
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyWindow?.Dispose();

        if (_singleInstanceMutex != null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
