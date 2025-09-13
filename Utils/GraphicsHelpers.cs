using System;
using System.Drawing;

namespace SimpleSpaceMongerCS
{
    public static class GraphicsHelpers
    {
        public static string HumanReadable(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            int place = (int)Math.Min(Math.Floor(Math.Log(bytes, 1024)), suf.Length - 1);
            double num = bytes / Math.Pow(1024, place);
            return string.Format("{0:0.##} {1}", num, suf[place]);
        }

        public static Color ColorFromString(string s, int depth)
        {
            int hash = Math.Abs(s.GetHashCode());
            float hue = hash % 360;
            float saturation = 0.5f;
            float lightness = Math.Max(0.45f, 0.8f - depth * 0.06f);
            return FromHsl(hue, saturation, lightness, 230);
        }

        public static Brush GetTextBrush(Color c)
        {
            double lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
            return (lum < 0.6) ? Brushes.White : Brushes.Black;
        }

        public static Color FromHsl(float h, float s, float l, int alpha = 255)
        {
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float hh = h / 60f;
            float x = c * (1 - Math.Abs(hh % 2 - 1));
            float rr = 0, gg = 0, bb = 0;
            if (hh >= 0 && hh < 1) { rr = c; gg = x; bb = 0; }
            else if (hh < 2) { rr = x; gg = c; bb = 0; }
            else if (hh < 3) { rr = 0; gg = c; bb = x; }
            else if (hh < 4) { rr = 0; gg = x; bb = c; }
            else if (hh < 5) { rr = x; gg = 0; bb = c; }
            else { rr = c; gg = 0; bb = x; }
            float m = l - c / 2;
            int R = (int)Math.Round((rr + m) * 255);
            int G = (int)Math.Round((gg + m) * 255);
            int B = (int)Math.Round((bb + m) * 255);
            R = Math.Max(0, Math.Min(255, R));
            G = Math.Max(0, Math.Min(255, G));
            B = Math.Max(0, Math.Min(255, B));
            return Color.FromArgb(alpha, R, G, B);
        }
    }
}
