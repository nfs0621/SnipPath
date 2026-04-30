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

        try
        {
            using var bitmap = CaptureBitmap(rect);
            if (_mode == SnipCaptureMode.CopyImage)
            {
                CopyImageToClipboard(bitmap);
            }
            else
            {
                var path = SaveCapture(bitmap);
                System.Windows.Clipboard.SetText(path);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Capture failed: {ex.Message}", "SnipPath");
        }

        Close();
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

    private static string SaveCapture(Bitmap bitmap)
    {
        var screenshotsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Screenshots");
        Directory.CreateDirectory(screenshotsDir);

        var filename = $"SnipPath_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var path = Path.Combine(screenshotsDir, filename);

        bitmap.Save(path, ImageFormat.Png);
        return path;
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
    private static extern IntPtr WindowFromPoint(DrawingPoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    private bool TryGetWindowRectFromCursor(out Rectangle rect)
    {
        rect = Rectangle.Empty;

        var cursorScreen = PointToScreen(Mouse.GetPosition(this));
        var cursorPoint = new DrawingPoint(
            (int)Math.Round(cursorScreen.X),
            (int)Math.Round(cursorScreen.Y));

        var hwnd = WindowFromPoint(cursorPoint);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (hwnd == _snipHandle)
        {
            hwnd = GetWindow(hwnd, GW_HWNDNEXT);
        }

        if (hwnd == IntPtr.Zero || hwnd == _snipHandle)
        {
            return false;
        }

        hwnd = GetAncestor(hwnd, GA_ROOT);
        if (hwnd == IntPtr.Zero || hwnd == _snipHandle)
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

    private static bool TryGetExtendedFrameBounds(IntPtr hwnd, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var rawRect, Marshal.SizeOf<Rect>()) != 0)
        {
            return false;
        }

        rect = new Rectangle(rawRect.Left, rawRect.Top, rawRect.Right - rawRect.Left, rawRect.Bottom - rawRect.Top);
        return rect.Width > 1 && rect.Height > 1;
    }

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint GW_HWNDNEXT = 2;
    private const uint GA_ROOT = 2;

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
