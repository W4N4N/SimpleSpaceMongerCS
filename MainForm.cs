using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleSpaceMongerCS
{
    public class MainForm : Form
    {
        private Button btnBrowse;
        private Panel drawPanel;
        private FolderBrowserDialog folderDialog;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Dictionary<string, long> sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private string rootPath;
        private long total;
        private List<(RectangleF rect, string path, long size, string name)> tileHitTest = new List<(RectangleF, string, long, string)>();
        private ToolTip hoverTip = new ToolTip();
        private string? lastHoverPath = null;
        private System.Windows.Forms.Timer hoverTimer;
        private string? pendingHoverPath = null;
        private Point pendingHoverPoint;

        public MainForm()
        {
            Text = "Simple SpaceMonger";
            Width = 1000;
            Height = 700;

            btnBrowse = new Button { Text = "Browse...", Dock = DockStyle.Top, Height = 30 };
            btnBrowse.Click += BtnBrowse_Click;

            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
            lblStatus = new Label { Text = "Ready", Dock = DockStyle.Top, Height = 18, TextAlign = ContentAlignment.MiddleLeft };

            // Use a double-buffered panel to ensure full redraw on resize/maximize
            drawPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White };
            drawPanel.Paint += DrawPanel_Paint;
            drawPanel.MouseMove += DrawPanel_MouseMove;
            drawPanel.MouseLeave += DrawPanel_MouseLeave;
            drawPanel.MouseClick += DrawPanel_MouseClick;
            hoverTimer = new System.Windows.Forms.Timer();
            hoverTimer.Interval = 150; // ms debounce
            hoverTimer.Tick += HoverTimer_Tick;

            Controls.Add(drawPanel);
            Controls.Add(lblStatus);
            Controls.Add(progressBar);
            Controls.Add(btnBrowse);

            folderDialog = new FolderBrowserDialog();
            rootPath = Directory.GetCurrentDirectory();

            // Initial async scan
            _ = ScanAndInvalidateAsync(rootPath);
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                rootPath = folderDialog.SelectedPath;
                _ = ScanAndInvalidateAsync(rootPath);
            }
        }

        private async Task ScanAndInvalidateAsync(string path)
        {
            sizes.Clear();
            total = 0;
            progressBar.Value = 0;
            lblStatus.Text = "Enumerating directories...";
            drawPanel.Invalidate();

            var progress = new Progress<int>(p =>
            {
                progressBar.Value = Math.Min(100, Math.Max(0, p));
                lblStatus.Text = $"Scanning... {p}%";
            });

            try
            {
                var result = await Task.Run(() => FileScanner.ScanPath(path, progress));
                sizes = result;
                sizes.TryGetValue(path, out total);
                lblStatus.Text = "Done";
                progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: " + ex.Message;
            }

            drawPanel.Invalidate();
        }

        private void DrawPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            tileHitTest.Clear();
            if (sizes == null || sizes.Count == 0)
            {
                g.DrawString(lblStatus.Text, Font, Brushes.Black, 10, 10);
                return;
            }

            Rectangle rect = drawPanel.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            // Gather immediate children of rootPath
            var rootChildren = new List<(string path, long size, string name)>();
            foreach (var kv in sizes)
            {
                if (kv.Key.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) continue;
                // compute a safe relative path; Path.GetRelativePath handles different path formats
                string rel = Path.GetRelativePath(rootPath, kv.Key);
                if (string.IsNullOrEmpty(rel) || rel == ".") continue;
                if (!rel.Contains(Path.DirectorySeparatorChar.ToString()))
                    rootChildren.Add((kv.Key, kv.Value, Path.GetFileName(kv.Key)));
            }

            if (rootChildren.Count == 0)
            {
                g.DrawString("No subfolders", Font, Brushes.Black, 10, 10);
                return;
            }

            var items = rootChildren.OrderByDescending(i => i.size).ToList();
            DrawTreemap(g, rect, items, 0);
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
                    inner.Inflate(-4, -4);
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

        private void DrawTile(Graphics g, RectangleF r, (string path, long size, string name) it, int depth)
        {
            var col = GraphicsHelpers.ColorFromString(it.path, depth);
            using (var brush = new SolidBrush(col)) g.FillRectangle(brush, r);
            using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0))) g.DrawRectangle(pen, Rectangle.Round(r));

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

            // record this tile for hit-testing
            tileHitTest.Add((r, it.path, it.size, it.name));
        }

        private void DrawPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            var p = e.Location;
            var found = tileHitTest.LastOrDefault(t => t.rect.Contains(p.X, p.Y));
            if (found.path != null)
            {
                pendingHoverPath = found.path;
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
            var tile = tileHitTest.LastOrDefault(t => t.rect.Contains(p.X, p.Y));
            if (tile.path == null) return;

            if (e.Button == MouseButtons.Left)
            {
                // Left click: behave like hover (show tooltip/details briefly)
                lastHoverPath = tile.path;
                hoverTip.Show($"{tile.name}\n{GraphicsHelpers.HumanReadable(tile.size)}\n{tile.path}", drawPanel, p.X + 15, p.Y + 15, 5000);
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Capture values locally so callbacks use the clicked tile values (avoid closure issues)
                string clickedPath = tile.path;
                long clickedSize = tile.size;
                string clickedName = tile.name;

                var cms = new ContextMenuStrip();
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
                    MessageBox.Show($"{clickedName}\n{GraphicsHelpers.HumanReadable(clickedSize)}\n{clickedPath}", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                var tile = tileHitTest.LastOrDefault(t => t.path == pendingHoverPath);
                if (tile.path != null)
                {
                    lastHoverPath = tile.path;
                    hoverTip.Show($"{tile.name}\n{GraphicsHelpers.HumanReadable(tile.size)}\n{tile.path}", drawPanel, pendingHoverPoint.X + 15, pendingHoverPoint.Y + 15, 5000);
                }
            }
        }

    }
}
