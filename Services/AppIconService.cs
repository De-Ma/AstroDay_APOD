using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingIcon = System.Drawing.Icon;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;

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
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(DrawingColor.Transparent);
            using var background = new LinearGradientBrush(new DrawingRectangle(3, 3, 58, 58), DrawingColor.FromArgb(255, 29, 61, 58), DrawingColor.FromArgb(255, 15, 27, 36), 135f);
            graphics.FillEllipse(background, 3, 3, 58, 58);
            using var orbit = new Pen(DrawingColor.FromArgb(220, 162, 226, 209), 2.4f);
            graphics.DrawEllipse(orbit, 11, 22, 42, 21);
            using var planet = new SolidBrush(DrawingColor.FromArgb(255, 223, 245, 232));
            graphics.FillEllipse(planet, 26, 19, 13, 13);
            using var star = new SolidBrush(DrawingColor.FromArgb(255, 255, 209, 137));
            graphics.FillEllipse(star, 43, 14, 5, 5);
            using var smallStar = new SolidBrush(DrawingColor.FromArgb(230, 230, 246, 241));
            graphics.FillEllipse(smallStar, 15, 42, 4, 4);
            graphics.DrawLine(orbit, new DrawingPointF(29, 5), new DrawingPointF(33, 13));
            graphics.DrawLine(orbit, new DrawingPointF(25, 9), new DrawingPointF(37, 9));
        }

        var handle = bitmap.GetHicon();
        try { return (DrawingIcon)DrawingIcon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }
}
