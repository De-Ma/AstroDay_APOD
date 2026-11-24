using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingIcon = System.Drawing.Icon;

namespace AstroDay.Services;

public static class AppIconService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static DrawingIcon CreateIcon() => (DrawingIcon)CreateDrawingIcon().Clone();

    public static BitmapSource CreateImageSource()
    {
        using var icon = CreateDrawingIcon();
        var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(64, 64));
        source.Freeze();
        return source;
    }

    private static DrawingIcon CreateDrawingIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(DrawingColor.Transparent);
            using var background = new SolidBrush(DrawingColor.FromArgb(255, 12, 20, 28));
            graphics.FillEllipse(background, 2, 2, 60, 60);
            using var orbit = new Pen(DrawingColor.FromArgb(255, 154, 219, 199), 4f);
            using var planet = new SolidBrush(DrawingColor.FromArgb(255, 207, 248, 232));
            using var moon = new SolidBrush(DrawingColor.FromArgb(255, 242, 190, 114));
            using var accent = new Pen(DrawingColor.FromArgb(255, 134, 189, 235), 1.2f);
            graphics.DrawEllipse(orbit, 11, 11, 42, 42);
            graphics.DrawArc(accent, 17, 17, 30, 30, 205, 70);
            graphics.FillEllipse(planet, 25, 25, 14, 14);
            graphics.FillEllipse(moon, 45, 43, 9, 9);
        }

        var handle = bitmap.GetHicon();
        try { return (DrawingIcon)DrawingIcon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }
}
