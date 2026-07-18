using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HueBar;

/// <summary>Draws the tray icon at runtime so the app ships no binary image asset.</summary>
internal static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>A simple white lightbulb silhouette with a faint outline so it reads on light taskbars too.</summary>
    public static Icon CreateBulbIcon(int size = 64)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float s = size;
            using var white = new SolidBrush(Color.White);
            using var outline = new Pen(Color.FromArgb(160, 70, 70, 70), Math.Max(1f, s / 40f));
            using var thread = new Pen(Color.FromArgb(170, 70, 70, 70), Math.Max(1f, s / 40f));

            // Glass bulb.
            var glass = new RectangleF(s * 0.20f, s * 0.08f, s * 0.60f, s * 0.60f);
            g.FillEllipse(white, glass);
            g.DrawEllipse(outline, glass);

            // Screw base.
            float bw = s * 0.30f, bx = (s - bw) / 2f;
            var baseRect = new RectangleF(bx, s * 0.60f, bw, s * 0.22f);
            using (var basePath = RoundedRect(baseRect, s * 0.04f))
            {
                g.FillPath(white, basePath);
                g.DrawPath(outline, basePath);
            }

            // Two thread grooves.
            g.DrawLine(thread, bx + s * 0.02f, s * 0.67f, bx + bw - s * 0.02f, s * 0.67f);
            g.DrawLine(thread, bx + s * 0.02f, s * 0.74f, bx + bw - s * 0.02f, s * 0.74f);

            // Contact tip.
            float tw = s * 0.14f, tx = (s - tw) / 2f;
            var tip = new RectangleF(tx, s * 0.80f, tw, s * 0.08f);
            using var tipPath = RoundedRect(tip, s * 0.03f);
            g.FillPath(white, tipPath);
            g.DrawPath(outline, tipPath);
        }

        // GetHicon() creates an unmanaged icon handle we must destroy after cloning a managed copy.
        IntPtr hicon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
