using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingPoint = System.Drawing.Point;
using WpfPoint = System.Windows.Point;

namespace SnipPath;

public partial class SnipWindow : Window
{
    private readonly Action _onClose;
    private readonly SnipCaptureMode _mode;
    private bool _isDragging;
    private bool _isClosing;
    private WpfPoint _startPoint;
    private IntPtr _snipHandle;

    public SnipWindow(Action onClose, SnipCaptureMode mode)
    {
        InitializeComponent();
        _onClose = onClose;
        _mode = mode;

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
        Deactivated += OnDeactivated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        _snipHandle = new WindowInteropHelper(this).Handle;
        Focus();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _startPoint = e.GetPosition(this);
        Selection.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_startPoint, _startPoint);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var current = e.GetPosition(this);
        UpdateSelection(_startPoint, current);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();

        var endPoint = e.GetPosition(this);
        var rect = BuildRect(_startPoint, endPoint);

        if (rect.Width < 2 || rect.Height < 2)
        {
            if (!TryGetWindowRectFromCursor(out rect))
            {
                Close();
                return;
            }
        }

        string? error = null;
        try
        {
            using var bitmap = CaptureBitmap(rect);
            if (_mode == SnipCaptureMode.CopyImage)
            {
                if (!TrySetClipboard(() => CopyImageToClipboard(bitmap)))
                {
                    var path = SaveCapture(bitmap);
                    error = $"Couldn't copy the image because another app is using the clipboard.\n\nThe snip was saved to:\n{path}";
                }
            }
            else
            {
                var path = SaveCapture(bitmap);
                if (!TrySetClipboard(() => System.Windows.Clipboard.SetText(path)))
                {
                    error = $"Couldn't copy the path because another app is using the clipboard.\n\nThe snip was saved to:\n{path}";
                }
            }
        }
        catch (Exception ex)
        {
            error = $"Capture failed: {ex.Message}";
        }

        // Close before showing any message so the overlay isn't left covering the screen.
        Close();

        if (error != null)
        {
            System.Windows.MessageBox.Show(error, "SnipPath");
        }
    }

    private const int ClipboardAttempts = 3;
    private const int ClipboardRetryDelayMs = 200;

    // Opening the clipboard fails while another app (clipboard managers, RDP, Office) holds it,
    // which is usually only for a moment.
    private static bool TrySetClipboard(Action setClipboard)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                setClipboard();
                return true;
            }
            catch (ExternalException) when (attempt < ClipboardAttempts)
            {
                Thread.Sleep(ClipboardRetryDelayMs);
            }
            catch (ExternalException)
            {
                return false;
            }
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _onClose();
        base.OnClosed(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        Deactivated -= OnDeactivated;
        base.OnClosing(e);
    }

    private Rectangle BuildRect(WpfPoint start, WpfPoint end)
    {
        var startScreen = ToScreenPoint(start);
        var endScreen = ToScreenPoint(end);
        return BuildRect(startScreen, endScreen);
    }

    private DrawingPoint ToScreenPoint(WpfPoint point)
    {
        var screenPoint = PointToScreen(point);
        return new DrawingPoint(
            (int)Math.Round(screenPoint.X),
            (int)Math.Round(screenPoint.Y));
    }

    private static Rectangle BuildRect(DrawingPoint start, DrawingPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(start.X - end.X);
        var height = Math.Abs(start.Y - end.Y);
        return new Rectangle(left, top, width, height);
    }

    private static Bitmap CaptureBitmap(Rectangle rect)
    {
        var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
        }

        return bitmap;
    }

    private static readonly TimeSpan CaptureRetention = TimeSpan.FromDays(7);

    // Snips are throwaway inputs for CLI tools, so keep them local in %TEMP% rather than
    // in Pictures, which is often synced to OneDrive.
    private static string SaveCapture(Bitmap bitmap)
    {
        var capturesDir = Path.Combine(Path.GetTempPath(), "SnipPath");
        Directory.CreateDirectory(capturesDir);
        DeleteOldCaptures(capturesDir);

        var filename = $"SnipPath_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var path = Path.Combine(capturesDir, filename);

        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    private static void DeleteOldCaptures(string capturesDir)
    {
        var cutoff = DateTime.Now - CaptureRetention;
        foreach (var file in Directory.EnumerateFiles(capturesDir, "SnipPath_*.png"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // File is in use (e.g. open in a viewer); try again on a later snip.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void CopyImageToClipboard(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            System.Windows.Clipboard.SetImage(source);
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private void UpdateSelection(WpfPoint start, WpfPoint current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(start.X - current.X);
        var height = Math.Abs(start.Y - current.Y);

        Canvas.SetLeft(Selection, x);
        Canvas.SetTop(Selection, y);
        Selection.Width = width;
        Selection.Height = height;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Close();
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    private bool TryGetWindowRectFromCursor(out Rectangle rect)
    {
        rect = Rectangle.Empty;

        var cursorScreen = PointToScreen(Mouse.GetPosition(this));
        var cursorPoint = new DrawingPoint(
            (int)Math.Round(cursorScreen.X),
            (int)Math.Round(cursorScreen.Y));

        var hwnd = FindWindowBelowSnip(cursorPoint);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!TryGetExtendedFrameBounds(hwnd, out rect))
        {
            if (!GetWindowRect(hwnd, out var rawRect))
            {
                return false;
            }

            rect = new Rectangle(rawRect.Left, rawRect.Top, rawRect.Right - rawRect.Left, rawRect.Bottom - rawRect.Top);
        }

        return rect.Width > 1 && rect.Height > 1;
    }

    // The overlay covers the whole screen, so WindowFromPoint only ever finds the overlay.
    // Walk the top-level windows below it in z-order and take the first one the user can see under the cursor.
    private IntPtr FindWindowBelowSnip(DrawingPoint point)
    {
        var hwnd = GetWindow(_snipHandle, GW_HWNDNEXT);
        while (hwnd != IntPtr.Zero)
        {
            if (IsCandidateWindow(hwnd, point))
            {
                return hwnd;
            }

            hwnd = GetWindow(hwnd, GW_HWNDNEXT);
        }

        return IntPtr.Zero;
    }

    private static bool IsCandidateWindow(IntPtr hwnd, DrawingPoint point)
    {
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        // Click-through overlays (e.g. GPU or recording overlays) can't be the window the user clicked on.
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TRANSPARENT) != 0)
        {
            return false;
        }

        // Cloaked windows (other virtual desktops, suspended UWP apps) report as visible but aren't on screen.
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out var rawRect))
        {
            return false;
        }

        return point.X >= rawRect.Left && point.X < rawRect.Right
            && point.Y >= rawRect.Top && point.Y < rawRect.Bottom;
    }

    private static bool TryGetExtendedFrameBounds(IntPtr hwnd, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out Rect rawRect, Marshal.SizeOf<Rect>()) != 0)
        {
            return false;
        }

        rect = new Rectangle(rawRect.Left, rawRect.Top, rawRect.Right - rawRect.Left, rawRect.Bottom - rawRect.Top);
        return rect.Width > 1 && rect.Height > 1;
    }

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;
    private const uint GW_HWNDNEXT = 2;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public enum SnipCaptureMode
{
    CopyPath,
    CopyImage
}
