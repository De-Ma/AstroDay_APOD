using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AstroDay.Services;

// A borderless WPF window maximizes over the taskbar unless the maximized bounds are limited to the work area.
public static class WorkAreaFix
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 2;

    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(Hook);
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo) return IntPtr.Zero;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var source = HwndSource.FromHwnd(hwnd);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        minMax.MinTrackSize = new Point32
        {
            X = (int)((System.Windows.Application.Current.MainWindow?.MinWidth ?? 960) * transform.M11),
            Y = (int)((System.Windows.Application.Current.MainWindow?.MinHeight ?? 650) * transform.M22)
        };
        minMax.MaxPosition = new Point32 { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
        minMax.MaxSize = new Point32 { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
        Marshal.StructureToPtr(minMax, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32 { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32 { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point32 Reserved;
        public Point32 MaxSize;
        public Point32 MaxPosition;
        public Point32 MinTrackSize;
        public Point32 MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect32 Monitor;
        public Rect32 Work;
        public int Flags;
    }
}
