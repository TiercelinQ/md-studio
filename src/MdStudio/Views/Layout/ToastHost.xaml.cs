using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace MdStudio.Views.Layout;

/// <summary>
/// Toast area at the bottom right of the window. Code-behind limited to layout: its width and bottom
/// margin depend on the window and on the screen, so they are computed at run time (rules/xaml.md
/// exception for values that cannot be a resource).
/// </summary>
public partial class ToastHost : UserControl
{
    private const uint MonitorDefaultToNearest = 2;

    private Window? _window;

    /// <summary>Initializes a new instance of the <see cref="ToastHost"/> class.</summary>
    public ToastHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null || Window.GetWindow(this) is not { } window)
        {
            return;
        }

        _window = window;
        window.SizeChanged += (_, _) => UpdatePlacement();
        window.LocationChanged += (_, _) => UpdatePlacement();
        window.StateChanged += (_, _) => UpdatePlacement();
        UpdatePlacement();
    }

    /// <summary>
    /// Width: ToastWidth, or the area width minus both margins when narrower (TS-11). Margins: ToastAreaMargin
    /// from the client area (TS-17), the bottom one raised by the part hidden under the taskbar (TS-16).
    /// </summary>
    private void UpdatePlacement()
    {
        if (Parent is not FrameworkElement area)
        {
            return;
        }

        var margin = (double)FindResource("ToastAreaMargin");
        Width = Math.Max(0, Math.Min((double)FindResource("ToastWidth"), area.ActualWidth - (2 * margin)));
        Margin = new Thickness(0, 0, margin, margin + HiddenBottom(area));
    }

    /// <summary>Height, in device-independent pixels, of the bottom of the client area lying below the work area of its screen.</summary>
    private double HiddenBottom(FrameworkElement area)
    {
        if (_window is null || PresentationSource.FromVisual(area) is not { CompositionTarget: { } target })
        {
            return 0;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle == 0 || !GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
        {
            return 0;
        }

        var bottom = area.PointToScreen(new Point(0, area.ActualHeight)).Y;
        var hidden = bottom - info.WorkBottom;
        return hidden > 0 ? hidden * target.TransformFromDevice.M22 : 0;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>MONITORINFO of user32: screen and work area rectangles in device pixels.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public int MonitorLeft;
        public int MonitorTop;
        public int MonitorRight;
        public int MonitorBottom;
        public int WorkLeft;
        public int WorkTop;
        public int WorkRight;
        public int WorkBottom;
        public uint Flags;
    }
}
