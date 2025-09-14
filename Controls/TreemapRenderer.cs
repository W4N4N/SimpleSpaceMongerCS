using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SimpleSpaceMongerCS.Controls
{
    public static class TreemapRenderer
    {
        // Draw a single tile (kept small and deterministic so it can be unit-tested if needed)
        public static void DrawTile(Graphics g, RectangleF r, string? path, long size, string name, int depth, ColorScheme scheme, ByPathPalette palette, long total)
        {
            bool isFree = path != null && path.EndsWith("|FREE|");
            bool isDrive = path != null && path.StartsWith("DRIVE:");

            if (isFree)
            {
                using (var brush = new SolidBrush(Color.White)) g.FillRectangle(brush, r);
                using (var pen = new Pen(Color.FromArgb(80, 200, 200, 200), 1))
                {
                    g.DrawRectangle(pen, Rectangle.Round(r));
                }
                return;
            }

            Color col;
            switch (scheme)
            {
                case ColorScheme.BySize:
                    double denom = Math.Max(1.0, (double)total);
                    double ratio = (double)size / denom;
                    float hue = (float)(ratio * 240.0);
                    col = GraphicsHelpers.FromHsl(hue, 0.6f, 0.55f, 230);
                    break;
                case ColorScheme.Monochrome:
                    {
                        double denom2 = Math.Max(1.0, (double)total);
                        int gval = 200 - (int)(Math.Min(1.0, (double)size / denom2) * 140);
                        gval = Math.Max(40, Math.Min(240, gval));
                        col = Color.FromArgb(255, gval, gval, gval);
                    }
                    break;
                case ColorScheme.Pastel:
                    col = GraphicsHelpers.FromHsl(Math.Abs((path ?? "").GetHashCode()) % 360, 0.35f, Math.Max(0.6f, 0.7f - depth * 0.02f), 230);
                    break;
                default:
                    int hash = Math.Abs((path ?? string.Empty).GetHashCode());
                    switch (palette)
                    {
                        case ByPathPalette.Grayscale:
                            int gval = 120 + (hash % 100);
                            gval = Math.Max(30, Math.Min(230, gval));
                            col = Color.FromArgb(255, gval, gval, gval);
                            break;
                        case ByPathPalette.Warm:
                            col = GraphicsHelpers.FromHsl(hash % 60, 0.7f, Math.Max(0.5f, 0.7f - depth * 0.02f), 230);
                            break;
                        case ByPathPalette.Cool:
                            col = GraphicsHelpers.FromHsl(180 + (hash % 100), 0.6f, Math.Max(0.5f, 0.7f - depth * 0.02f), 230);
                            break;
                        case ByPathPalette.Pastel:
                            col = GraphicsHelpers.FromHsl(hash % 360, 0.35f, Math.Max(0.65f, 0.78f - depth * 0.02f), 230);
                            break;
                        default:
                            col = GraphicsHelpers.FromHsl(hash % 360, 0.6f, Math.Max(0.5f, 0.7f - depth * 0.02f), 230);
                            break;
                    }
                    break;
            }

            using (var brush = new SolidBrush(col)) g.FillRectangle(brush, r);
            int penWidth = Math.Min(4, 1 + depth / 2);
            using (var pen = new Pen(Color.FromArgb(110, 100, 100, 100), penWidth))
            {
                var rr = Rectangle.Round(r);
                g.DrawRectangle(pen, rr);
            }

            bool hasChildren = false;
            try
            {
                if (!string.IsNullOrEmpty(path) && !path.StartsWith("DRIVE:") && !path.EndsWith("|FREE|"))
                {
                    // hasChildren cannot be reliably known by renderer; caller can pass metadata if desired
                }
            }
            catch { }

            if (isDrive)
            {
                try
                {
                    Icon icon = SystemIcons.Application;
                    var iconRect = new Rectangle((int)r.X + 4, (int)r.Y + 4, 16, 16);
                    g.DrawIcon(icon, iconRect);
                }
                catch { }
            }

            string label = name;
            string info = GraphicsHelpers.HumanReadable(size);
            var font = SystemFonts.DefaultFont;
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            var text = label + " " + info;
            var sizeF = g.MeasureString(text, font);
            if (r.Width > 2 && r.Height > 2)
            {
                if (sizeF.Width < r.Width && sizeF.Height < r.Height)
                {
                    var textBrush = GraphicsHelpers.GetTextBrush(col);
                    g.DrawString(text, font, textBrush, r, sf);
                }
            }

            // selection and hit-testing should be handled by caller
        }
    }
}
