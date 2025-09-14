using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SimpleSpaceMongerCS
{
    public partial class MainForm
    {
        private void DrawPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            var p = e.Location;
            lastMousePos = p;
            var found = tileHitTest.LastOrDefault(t => t.Rect.Contains(p.X, p.Y));
            if (found.Path != null)
            {
                pendingHoverPath = found.Path;
                pendingHoverPoint = p;
                hoverTimer.Stop();
                hoverTimer.Start();
            }
            else
            {
                pendingHoverPath = null;
                hoverTimer.Stop();
                hoverTip.Hide(drawPanel);
                lastHoverPath = null;
            }
        }

        private void DrawPanel_MouseLeave(object? sender, EventArgs e)
        {
            pendingHoverPath = null;
            hoverTimer.Stop();
            hoverTip.Hide(drawPanel);
            lastHoverPath = null;
        }

        private void DrawPanel_MouseClick(object? sender, MouseEventArgs e)
        {
            var p = e.Location;
            var tile = tileHitTest.LastOrDefault(t => t.Rect.Contains(p.X, p.Y));
            if (tile.Path == null)
            {
                // clicked outside any tile - clear selection
                selectedPath = null;
                drawPanel.Invalidate();
                return;
            }

            bool isDrive = tile.Path.StartsWith("DRIVE:");
            if (e.Button == MouseButtons.Left)
            {
                // If this is a drive tile, start scanning it on left-click
                if (isDrive)
                {
                    string actualPath = tile.Path.Substring("DRIVE:".Length);
                    rootPath = actualPath;
                    selectedPath = null; // change of root clears selection
                    _ = ScanAndInvalidateAsync(rootPath);
                    return;
                }

                // Otherwise behave like hover (show tooltip/details briefly)
                lastHoverPath = tile.Path;
                // fast immediate feedback: draw overlay immediately and invalidate only affected region
                string? oldSelected = selectedPath;
                RectangleF oldRect = RectangleF.Empty;
                if (!string.IsNullOrEmpty(oldSelected))
                {
                    var oldTile = tileHitTest.LastOrDefault(t => t.Path == oldSelected);
                    oldRect = oldTile.Rect;
                }
                selectedPath = tile.Path; // mark as selected
                // Invalidate only the union of old and new selection for permanent redraw (cached bitmap paints fast)
                var union = oldRect.IsEmpty ? tile.Rect : RectangleF.Union(oldRect, tile.Rect);
                try { drawPanel.Invalidate(Rectangle.Round(union)); } catch { drawPanel.Invalidate(); }
                bool isFree = tile.Path.EndsWith("|FREE|");
                if (isFree)
                {
                    double pct = total > 0 ? (tile.Size * 100.0) / total : 0.0;
                    string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                    hoverTip.Show($"{tile.Name}\n{GraphicsHelpers.HumanReadable(tile.Size)}{pctStr}", drawPanel, p.X + 15, p.Y + 15, 5000);
                }
                else
                {
                    double pct = total > 0 ? (tile.Size * 100.0) / total : 0.0;
                    string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                    hoverTip.Show($"{tile.Name}\n{GraphicsHelpers.HumanReadable(tile.Size)}{pctStr}\n{tile.Path}", drawPanel, p.X + 15, p.Y + 15, 5000);
                }
                // Note: we intentionally do NOT draw a transient overlay using CreateGraphics here
                // because the UI now relies on the cached bitmap paint path for immediate and
                // consistent feedback. Drawing directly with CreateGraphics can be overwritten by
                // subsequent paints and causes visible flicker.
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Capture values locally so callbacks use the clicked tile values (avoid closure issues)
                string clickedPath = tile.Path;
                long clickedSize = tile.Size;
                string clickedName = tile.Name;
                bool isFree = clickedPath.EndsWith("|FREE|");
                // reuse outer isDrive variable

                // Mark as selected when right-clicking as well
                selectedPath = clickedPath;
                drawPanel.Invalidate();

                var cms = new ContextMenuStrip();

                if (isDrive)
                {
                    // For drives, allow open and details and zoom in (scan this drive)
                    string actualPath = clickedPath.Substring("DRIVE:".Length);
                    cms.Items.Add("Open in Explorer", null, (s, a) =>
                    {
                        try
                        {
                            if (Directory.Exists(actualPath)) Process.Start("explorer.exe", actualPath);
                            else MessageBox.Show("Path not found.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        catch (Exception ex) { MessageBox.Show("Failed to open: " + ex.Message); }
                    });

                    cms.Items.Add("Details", null, (s, a) =>
                    {
                        double pct = total > 0 ? (clickedSize * 100.0) / total : 0.0;
                        string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                        MessageBox.Show($"{clickedName}\n{GraphicsHelpers.HumanReadable(clickedSize)}{pctStr}\n{actualPath}", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });

                    cms.Items.Add("Scan this disk", null, (s, a) =>
                    {
                        rootPath = actualPath;
                        _ = ScanAndInvalidateAsync(rootPath);
                    });
                }
                else if (!isFree)
                {
                    cms.Items.Add("Open in Explorer", null, (s, a) =>
                    {
                        try
                        {
                            if (Directory.Exists(clickedPath)) Process.Start("explorer.exe", clickedPath);
                            else if (File.Exists(clickedPath)) Process.Start("explorer.exe", "/select,\"" + clickedPath + "\"");
                            else MessageBox.Show("Path not found.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        catch (Exception ex) { MessageBox.Show("Failed to open: " + ex.Message); }
                    });

                    cms.Items.Add("Details", null, (s, a) =>
                    {
                        double pct = total > 0 ? (clickedSize * 100.0) / total : 0.0;
                        string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                        MessageBox.Show($"{clickedName}\n{GraphicsHelpers.HumanReadable(clickedSize)}{pctStr}\n{clickedPath}", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });

                    cms.Items.Add("Zoom In", null, (s, a) =>
                    {
                        try
                        {
                            if (Directory.Exists(clickedPath))
                            {
                                rootPath = clickedPath;
                                _ = ScanAndInvalidateAsync(rootPath);
                            }
                            else if (File.Exists(clickedPath))
                            {
                                MessageBox.Show("Cannot zoom into a file. Select its containing folder.", "Zoom In", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex) { MessageBox.Show("Zoom failed: " + ex.Message); }
                    });
                }
                else
                {
                    // For free-space tile only show Details
                    cms.Items.Add("Details", null, (s, a) =>
                    {
                        double pct = total > 0 ? (clickedSize * 100.0) / total : 0.0;
                        string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                        MessageBox.Show($"{clickedName}\n{GraphicsHelpers.HumanReadable(clickedSize)}{pctStr}", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }

                cms.Items.Add("Zoom Out", null, (s, a) =>
                {
                    try
                    {
                        var parent = Path.GetDirectoryName(rootPath);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            rootPath = parent;
                            _ = ScanAndInvalidateAsync(rootPath);
                        }
                        else
                        {
                            MessageBox.Show("No parent folder to zoom out to.", "Zoom Out", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show("Zoom failed: " + ex.Message); }
                });

                cms.Show(drawPanel, p);
            }
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (pendingHoverPath != null && pendingHoverPath != lastHoverPath)
            {
                var tile = tileHitTest.LastOrDefault(t => t.Path == pendingHoverPath);
                if (tile.Path != null)
                {
                    lastHoverPath = tile.Path;
                    bool isFree = tile.Path.EndsWith("|FREE|");
                    double pct = total > 0 ? (tile.Size * 100.0) / total : 0.0;
                    string pctStr = pct >= 0 ? $" ({pct:0.##}% of total)" : string.Empty;
                    string text = isFree ? $"{tile.Name}\n{GraphicsHelpers.HumanReadable(tile.Size)}{pctStr}" : $"{tile.Name}\n{GraphicsHelpers.HumanReadable(tile.Size)}{pctStr}\n{tile.Path}";
                    hoverTip.Show(text, drawPanel, pendingHoverPoint.X + 15, pendingHoverPoint.Y + 15, 5000);
                }
            }
        }
    }
}
