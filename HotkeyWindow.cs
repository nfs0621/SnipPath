using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace SnipPath;

public sealed class HotkeyWindow : Window, IDisposable
{
    private const int HotkeySnipId = 0x0001;
    private const int HotkeyQuitId = 0x0002;
    private const int HotkeySnipClipboardId = 0x0003;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;

    private HwndSource? _source;
    private bool _snipActive;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public HotkeyWindow()
    {
        Width = 0;
        Height = 0;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        Opacity = 0;
        Left = -10000;
        Top = -10000;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        _source.AddHook(WndProc);

        var vkSnip = KeyInterop.VirtualKeyFromKey(Key.S);
        if (!RegisterHotKey(_source.Handle, HotkeySnipId, MOD_CONTROL | MOD_SHIFT, (uint)vkSnip))
        {
            System.Windows.MessageBox.Show("Failed to register hotkey Ctrl+Shift+S.", "SnipPath");
        }

        var vkClipboard = KeyInterop.VirtualKeyFromKey(Key.C);
        if (!RegisterHotKey(_source.Handle, HotkeySnipClipboardId, MOD_CONTROL | MOD_SHIFT, (uint)vkClipboard))
        {
            System.Windows.MessageBox.Show("Failed to register hotkey Ctrl+Shift+C.", "SnipPath");
        }

        var vkQuit = KeyInterop.VirtualKeyFromKey(Key.Q);
        if (!RegisterHotKey(_source.Handle, HotkeyQuitId, MOD_CONTROL | MOD_SHIFT, (uint)vkQuit))
        {
            System.Windows.MessageBox.Show("Failed to register hotkey Ctrl+Shift+Q.", "SnipPath");
        }

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "SnipPath",
            Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = BuildTrayMenu()
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, HotkeySnipId);
            UnregisterHotKey(_source.Handle, HotkeySnipClipboardId);
            UnregisterHotKey(_source.Handle, HotkeyQuitId);
            _source.RemoveHook(WndProc);
            _source = null;
        }

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        base.OnClosed(e);
    }

    public void Dispose()
    {
        Close();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeySnipId)
        {
            StartSnip(SnipCaptureMode.CopyPath);

            handled = true;
        }
        else if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeySnipClipboardId)
        {
            StartSnip(SnipCaptureMode.CopyImage);
            handled = true;
        }
        else if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyQuitId)
        {
            System.Windows.Application.Current.Shutdown();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static System.Windows.Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var snipItem = new System.Windows.Forms.ToolStripMenuItem("Snip (Ctrl+Shift+S)");
        snipItem.Click += (_, _) =>
        {
            var app = (App)System.Windows.Application.Current;
            app.Dispatcher.Invoke(() =>
            {
                var window = app.Windows.OfType<HotkeyWindow>().FirstOrDefault();
                window?.StartSnip(SnipCaptureMode.CopyPath);
            });
        };

        var clipboardItem = new System.Windows.Forms.ToolStripMenuItem("Snip to Clipboard (Ctrl+Shift+C)");
        clipboardItem.Click += (_, _) =>
        {
            var app = (App)System.Windows.Application.Current;
            app.Dispatcher.Invoke(() =>
            {
                var window = app.Windows.OfType<HotkeyWindow>().FirstOrDefault();
                window?.StartSnip(SnipCaptureMode.CopyImage);
            });
        };

        var quitItem = new System.Windows.Forms.ToolStripMenuItem("Quit (Ctrl+Shift+Q)");
        quitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(snipItem);
        menu.Items.Add(clipboardItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(quitItem);
        return menu;
    }

    internal void StartSnip(SnipCaptureMode mode)
    {
        if (_snipActive)
        {
            return;
        }

        _snipActive = true;
        try
        {
            var snip = new SnipWindow(() => _snipActive = false, mode);
            snip.Show();
            snip.Activate();
        }
        catch (Exception ex)
        {
            _snipActive = false;
            System.Windows.MessageBox.Show($"Failed to start snip: {ex.Message}", "SnipPath");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
