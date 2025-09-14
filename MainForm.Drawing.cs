using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SimpleSpaceMongerCS
{
    public partial class MainForm
    {
        // Hit-test entry for tiles (replaces tuple usage for clarity)
        private readonly record struct TileHit(RectangleF Rect, string? Path, long Size, string Name);

        // Cached layout and pre-rendered bitmap
        private List<TreemapLayout.TileLayout> cachedLayout = new List<TreemapLayout.TileLayout>();
        private Bitmap? cachedBitmap = null;
        private volatile bool layoutStale = true;
        private readonly object layoutLock = new object();

        // Rebuild cached layout and the pre-rendered bitmap. Call on scan completion or ResizeEnd.
        private void RebuildLayoutAndBitmap()
        {
            lock (layoutLock)
            {
                try
                {
                    layoutStale = true;
                    cachedLayout.Clear();
                    tileHitTest.Clear();

                    if (rootPath == "__DRIVES__")
                    {
                        // no cached bitmap for drives overview
                        cachedBitmap?.Dispose(); cachedBitmap = null;
                        layoutStale = false;
                        return;
                    }

                    if (sizes == null || sizes.Count == 0)
                    {
                        cachedBitmap?.Dispose(); cachedBitmap = null;
                        layoutStale = false;
                        return;
                    }

                    Rectangle area = drawPanel.ClientRectangle;
                    if (area.Width <= 0 || area.Height <= 0)
                    {
                        cachedBitmap?.Dispose(); cachedBitmap = null;
                        layoutStale = false;
                        return;
                    }

                    // Build immediate children list (same logic as paint)
                    var rootChildren = new List<(string path, long size, string name)>();
                    foreach (var kv in sizes)
                    {
                        if (kv.Key.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) continue;
                        string rel = Path.GetRelativePath(rootPath, kv.Key);
                        if (string.IsNullOrEmpty(rel) || rel == ".") continue;
                        if (!rel.Contains(Path.DirectorySeparatorChar.ToString()))
                            rootChildren.Add((kv.Key, kv.Value, Path.GetFileName(kv.Key)));
                    }

                    if (rootChildren.Count == 0)
                    {
                        cachedBitmap?.Dispose(); cachedBitmap = null;
                        layoutStale = false;
                        return;
                    }

                    var items = rootChildren.OrderByDescending(i => i.size).ToList();

                    long sumChildren = items.Sum(i => i.size);
                    long freeSpace = 0;
                    try
                    {
                        var root = Path.GetPathRoot(rootPath ?? string.Empty) ?? string.Empty;
                        if (!string.IsNullOrEmpty(root) && string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), rootPath?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var di = new DriveInfo(root);
                                if (di.IsReady) freeSpace = Math.Max(0L, di.AvailableFreeSpace);
                            }
                            catch { }
                        }
                    }
                    catch { }

                    if (freeSpace == 0)
                        freeSpace = Math.Max(0L, total - sumChildren);

                    if (freeSpace > 0)
                    {
                        items.Add((path: rootPath + "|FREE|", size: freeSpace, name: "Free space"));
                        items = items.OrderByDescending(i => i.size).ToList();
                    }

                    // Build layout rectangles into cachedLayout via TreemapLayout
                    cachedLayout = TreemapLayout.BuildLayout(area, items);

                    // Create bitmap and render into it
                    cachedBitmap?.Dispose();
                    cachedBitmap = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height));
                    using (var g = Graphics.FromImage(cachedBitmap))
                    {
                        g.Clear(Color.White);
                        // draw tiles from cachedLayout using DrawTile (which will also populate tileHitTest)
                        foreach (var tl in cachedLayout)
                        {
                            DrawTile(g, tl.Rect, (tl.Path, tl.Size, tl.Name), tl.Depth);
                        }
                    }

                    layoutStale = false;
                }
                catch { layoutStale = true; }
            }
        }

        private void DrawPanel_Paint(object? sender, PaintEventArgs e)
        {
            // Short-circuit heavy drawing while the user is actively resizing to avoid freezing the UI
            if (isResizing)
            {
                e.Graphics.Clear(Color.White);
                return;
            }
            // If we're in drives overview mode, show each logical drive as a grid of icons (like Explorer)
            if (rootPath == "__DRIVES__")
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                tileHitTest.Clear();

                var drives = DriveInfo.GetDrives().Where(d => d.IsReady)
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d =>
                {
                    long size = d.TotalSize > 0 ? d.TotalSize : 1;
                    string letter = d.Name.TrimEnd('\\'); // e.g. "C:"
                    string label = string.IsNullOrEmpty(d.VolumeLabel) ? letter : $"{letter}: {d.VolumeLabel}";
                    return (drive: d, path: "DRIVE:" + d.Name, size: size, name: label);
                }).ToList();

                if (drives.Count == 0)
                {
                    g.DrawString("No drives found", Font, Brushes.Black, 10, 10);
                    return;
                }

                int padding = 12;
                int iconSize = 64;
                int labelHeight = 20;
                int cellWidth = iconSize + padding * 2;
                int clientWidth = drawPanel.ClientSize.Width;
                int cols = Math.Max(1, clientWidth / cellWidth);
                int x0 = padding;
                int y0 = padding;

                for (int i = 0; i < drives.Count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = x0 + col * cellWidth;
                    int y = y0 + row * (iconSize + labelHeight + padding);

                    var iconRect = new RectangleF(x, y, iconSize, iconSize);

                    // Draw icon
                    if (diskIcon != null)
                    {
                        g.DrawImage(diskIcon, Rectangle.Round(iconRect));
                    }
                    else
                    {
                        try { g.DrawIcon(SystemIcons.Application, Rectangle.Round(iconRect)); } catch { }
                    }

                    // Draw label centered under icon
                    var label = drives[i].name;
                    var labelRect = new RectangleF(x, y + iconSize, iconSize, labelHeight);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                    g.DrawString(label, this.Font, Brushes.Black, labelRect, sf);

                    // Hit-test area (icon + label)
                    var hitRect = new RectangleF(x - padding / 2, y - padding / 2, iconSize + padding, iconSize + labelHeight + padding);
                    tileHitTest.Add(new TileHit(hitRect, drives[i].path, drives[i].size, drives[i].name));
                }

                return;
            }

            var g2 = e.Graphics;
            g2.Clear(Color.White);
            tileHitTest.Clear();
            // If we have a valid cached bitmap matching the panel size, draw it for fast paint
            if (cachedBitmap != null && cachedBitmap.Width == drawPanel.ClientSize.Width && cachedBitmap.Height == drawPanel.ClientSize.Height && !layoutStale)
            {
                g2.DrawImageUnscaled(cachedBitmap, 0, 0);
                // repopulate hit-test entries from the cached layout so clicks/hovers work
                tileHitTest.Clear();
                try
                {
                    foreach (var tl in cachedLayout)
                    {
                        tileHitTest.Add(new TileHit(tl.Rect, tl.Path, tl.Size, tl.Name));
                    }
                }
                catch { }
                // Draw persistent selection overlay if a tile is selected
                try
                {
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        var sel = cachedLayout.FirstOrDefault(x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
                        if (sel != null && sel.Rect.Width > 1 && sel.Rect.Height > 1)
                        {
                            using (var selBrush = new SolidBrush(Color.FromArgb(56, 255, 215, 64)))
                            {
                                g2.FillRectangle(selBrush, sel.Rect);
                            }
                            using (var selPen = new Pen(Color.FromArgb(220, 255, 140, 0), 4))
                            {
                                var selRect = Rectangle.Round(new RectangleF(sel.Rect.X + 1, sel.Rect.Y + 1, Math.Max(0, sel.Rect.Width - 2), Math.Max(0, sel.Rect.Height - 2)));
                                g2.DrawRectangle(selPen, selRect);
                            }
                        }
                    }
                }
                catch { }
                // tileHitTest was populated during bitmap render
                // Draw selection/overlays on top
                // (the code below will handle parent highlight and other overlays)
            }
            else
            {
                // No cached bitmap — fall back to full draw to preserve behavior
                if (sizes == null || sizes.Count == 0)
                {
                    g2.DrawString(lblStatus.Text, Font, Brushes.Black, 10, 10);
                    return;
                }

                Rectangle rect = drawPanel.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                // Rebuild layout synchronously if missing so first display is correct
                RebuildLayoutAndBitmap();
                if (cachedBitmap != null)
                {
                    g2.DrawImageUnscaled(cachedBitmap, 0, 0);
                }
                else
                {
                    // If still no bitmap, fallback to original DrawTreemap path
                    Rectangle rect2 = drawPanel.ClientRectangle;
                    var rootChildren = new List<(string path, long size, string name)>();
                    foreach (var kv in sizes)
                    {
                        if (kv.Key.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) continue;
                        string rel = Path.GetRelativePath(rootPath, kv.Key);
                        if (string.IsNullOrEmpty(rel) || rel == ".") continue;
                        if (!rel.Contains(Path.DirectorySeparatorChar.ToString()))
                            rootChildren.Add((kv.Key, kv.Value, Path.GetFileName(kv.Key)));
                    }
                    var items = rootChildren.OrderByDescending(i => i.size).ToList();
                    DrawTreemap(g2, rect2, items, 0);
                }
            }

            // Draw a subtle highlight around the parent tile (second-innermost) under the mouse to aid clicking
            try
            {
                if (!lastMousePos.IsEmpty && tileHitTest.Count > 0)
                {
                    var matches = tileHitTest.Where(t => t.Rect.Contains(lastMousePos.X, lastMousePos.Y)).ToList();
                    if (matches.Count >= 2)
                    {
                        var parent = matches[matches.Count - 2];
                        var parentRect = parent.Rect;
                        using (var brush = new SolidBrush(Color.FromArgb(40, 0, 120, 215)))
                        {
                            g2.FillRectangle(brush, parentRect);
                        }
                        using (var pen = new Pen(Color.FromArgb(200, 0, 120, 215), 3))
                        {
                            g2.DrawRectangle(pen, Rectangle.Round(parentRect));
                        }
                    }
                }
            }
            catch { }

            return;
        }

        // Improved treemap using recursive binary partitioning for better aspect ratios
        private void DrawTreemap(Graphics g, Rectangle area, List<(string path, long size, string name)> items, int depth)
        {
            if (items == null || items.Count == 0) return;
            items = items.OrderByDescending(i => i.size).ToList();

            if (items.Count == 1)
            {
                var it = items[0];
                RectangleF rf = area;
                DrawTile(g, rf, it, depth);

                // draw one level of children inside if present
                var children = sizes.Where(kv => kv.Key.StartsWith(it.path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    .Where(kv =>
                    {
                        string rel = Path.GetRelativePath(it.path, kv.Key);
                        return !string.IsNullOrEmpty(rel) && rel != "." && !rel.Contains(Path.DirectorySeparatorChar.ToString());
                    })
                    .Select(kv => (path: kv.Key, size: kv.Value, name: Path.GetFileName(kv.Key))).OrderByDescending(c => c.size).ToList();

                if (children.Count > 0)
                {
                    Rectangle inner = Rectangle.Round(rf);
                    // Increase padding with depth so nested tiles don't tightly overlap parent borders
                    // Reduced values to make padding less aggressive
                    int pad = 6;
                    inner.Inflate(-pad, -pad);
                    if (inner.Width > 0 && inner.Height > 0)
                        DrawTreemap(g, inner, children, depth + 1);
                }
                return;
            }

            long totalSize = items.Sum(i => i.size);
            // split items into two groups with roughly equal sum
            long sum = 0; int splitIndex = 0;
            for (int i = 0; i < items.Count; i++)
            {
                sum += items[i].size;
                if (sum >= totalSize / 2)
                {
                    splitIndex = i;
                    break;
                }
            }

            var first = items.Take(splitIndex + 1).ToList();
            var second = items.Skip(splitIndex + 1).ToList();

            if (area.Width >= area.Height)
            {
                float ratio = totalSize > 0 ? (float)first.Sum(x => x.size) / totalSize : 0.5f;
                int w1 = Math.Max(1, (int)Math.Round(area.Width * ratio));
                var r1 = new Rectangle(area.X, area.Y, w1, area.Height);
                var r2 = new Rectangle(area.X + w1, area.Y, area.Width - w1, area.Height);
                DrawTreemap(g, r1, first, depth);
                DrawTreemap(g, r2, second, depth);
            }
            else
            {
                float ratio = totalSize > 0 ? (float)first.Sum(x => x.size) / totalSize : 0.5f;
                int h1 = Math.Max(1, (int)Math.Round(area.Height * ratio));
                var r1 = new Rectangle(area.X, area.Y, area.Width, h1);
                var r2 = new Rectangle(area.X, area.Y + h1, area.Width, area.Height - h1);
                DrawTreemap(g, r1, first, depth);
                DrawTreemap(g, r2, second, depth);
            }
        }

        private void DrawTile(Graphics g, RectangleF r, (string? path, long size, string name) it, int depth)
        {
            bool isFree = it.path != null && it.path.EndsWith("|FREE|");
            bool isDrive = it.path != null && it.path.StartsWith("DRIVE:");

            if (isFree)
            {
                // Render free space as a clean white block (no label), proportional in area
                using (var brush = new SolidBrush(Color.White)) g.FillRectangle(brush, r);
                using (var pen = new Pen(Color.FromArgb(80, 200, 200, 200), 1))
                {
                    g.DrawRectangle(pen, Rectangle.Round(r));
                }
                tileHitTest.Add(new TileHit(r, it.path, it.size, it.name));
                return;
            }

            Color col;
            switch (currentColorScheme)
            {
                case ColorScheme.BySize:
                    double denom = Math.Max(1.0, (double)total);
                    double ratio = (double)it.size / denom;
                    float hue = (float)(ratio * 240.0); // blue-ish scale
                    col = GraphicsHelpers.FromHsl(hue, 0.6f, 0.55f, 230);
                    break;
                case ColorScheme.Monochrome:
                    {
                        double denom2 = Math.Max(1.0, (double)total);
                        int gval = 200 - (int)(Math.Min(1.0, (double)it.size / denom2) * 140);
                        gval = Math.Max(40, Math.Min(240, gval));
                        col = Color.FromArgb(255, gval, gval, gval);
                    }
                    break;
                case ColorScheme.Pastel:
                    col = GraphicsHelpers.FromHsl(Math.Abs((it.path ?? "").GetHashCode()) % 360, 0.35f, Math.Max(0.6f, 0.7f - depth * 0.02f), 230);
                    break;
                default:
                    // By-Path coloring with selectable palettes
                    int hash = Math.Abs((it.path ?? string.Empty).GetHashCode());
                    switch (currentByPathPalette)
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
                            // Rainbow default
                            col = GraphicsHelpers.FromHsl(hash % 360, 0.6f, Math.Max(0.5f, 0.7f - depth * 0.02f), 230);
                            break;
                    }
                    break;
            }
            using (var brush = new SolidBrush(col)) g.FillRectangle(brush, r);
            int penWidth = Math.Min(4, 1 + depth / 2); // modest thickening for deeper tiles
            using (var pen = new Pen(Color.FromArgb(110, 100, 100, 100), penWidth))
            {
                var rr = Rectangle.Round(r);
                g.DrawRectangle(pen, rr);
            }

            // If this tile has child folders, draw a stronger outer border to make parent boundaries obvious
            bool hasChildren = false;
            try
            {
                if (!string.IsNullOrEmpty(it.path) && !it.path.StartsWith("DRIVE:") && !it.path.EndsWith("|FREE|"))
                {
                    hasChildren = sizes.Keys.Any(k => k.StartsWith(it.path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }

            if (hasChildren)
            {
                // Slightly thicker, darker outer border
                int extraWidth = Math.Max(2, penWidth + 1);
                using (var outerPen = new Pen(Color.FromArgb(200, 40, 40, 40), extraWidth))
                {
                    var outerRect = Rectangle.Round(new RectangleF(r.X + 1, r.Y + 1, Math.Max(0, r.Width - 2), Math.Max(0, r.Height - 2)));
                    g.DrawRectangle(outerPen, outerRect);
                }

                // For top-level parents make the border more prominent with a subtle tint
                if (depth == 0)
                {
                    using (var tint = new Pen(Color.FromArgb(80, 0, 120, 215), 3))
                    {
                        var tintRect = Rectangle.Round(new RectangleF(r.X + 2, r.Y + 2, Math.Max(0, r.Width - 4), Math.Max(0, r.Height - 4)));
                        g.DrawRectangle(tint, tintRect);
                    }
                }
            }

            // Draw a small drive icon for drive tiles
            if (isDrive)
            {
                try
                {
                    Icon icon = SystemIcons.Application; // fallback icon
                    var iconRect = new Rectangle((int)r.X + 4, (int)r.Y + 4, 16, 16);
                    g.DrawIcon(icon, iconRect);
                }
                catch { }
            }

            string label = it.name;
            string info = GraphicsHelpers.HumanReadable(it.size);
            var font = this.Font;
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            var text = label + " " + info;
            var sizeF = g.MeasureString(text, font);
            if (r.Width > 2 && r.Height > 2)
            {
                if (sizeF.Width < r.Width && sizeF.Height < r.Height)
                {
                    var textBrush = GraphicsHelpers.GetTextBrush(col); // do not dispose Brushes.* singletons
                    g.DrawString(text, font, textBrush, r, sf);
                }
            }

            // If this tile is currently selected (clicked), draw a translucent highlight and a stronger border
            try
            {
                if (!string.IsNullOrEmpty(selectedPath) && !string.IsNullOrEmpty(it.path) && string.Equals(selectedPath, it.path, StringComparison.OrdinalIgnoreCase))
                {
                    using (var selBrush = new SolidBrush(Color.FromArgb(56, 255, 215, 64))) // translucent warm highlight
                    {
                        g.FillRectangle(selBrush, r);
                    }
                    int selPenWidth = Math.Max(3, penWidth + 2);
                    using (var selPen = new Pen(Color.FromArgb(220, 255, 140, 0), selPenWidth))
                    {
                        var selRect = Rectangle.Round(new RectangleF(r.X + 1, r.Y + 1, Math.Max(0, r.Width - 2), Math.Max(0, r.Height - 2)));
                        g.DrawRectangle(selPen, selRect);
                    }
                }
            }
            catch { }

            // record this tile for hit-testing
            tileHitTest.Add(new TileHit(r, it.path, it.size, it.name));
        }

        // Removed DrawSelectionOverlayImmediate: ephemeral CreateGraphics overlays caused flicker and
        // inconsistent visuals with the cached bitmap approach. Selection is now drawn persistently
        // during Paint via the cached bitmap path.
    }
}
