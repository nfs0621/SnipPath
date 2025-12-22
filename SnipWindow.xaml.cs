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
    private DrawingPoint _startScreen;

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
        Focus();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _startPoint = e.GetPosition(this);
        _startScreen = System.Windows.Forms.Control.MousePosition;
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

        var endScreen = System.Windows.Forms.Control.MousePosition;
        var rect = BuildRect(_startScreen, endScreen);

        if (rect.Width < 2 || rect.Height < 2)
        {
            Close();
            return;
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
}

public enum SnipCaptureMode
{
    CopyPath,
    CopyImage
}
