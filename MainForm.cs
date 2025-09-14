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
    public partial class MainForm : Form
    {
        private MenuStrip menuStrip;
        private Panel drawPanel;
        private FolderBrowserDialog folderDialog;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Dictionary<string, long> sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private string rootPath;
        private long total;
        private List<(RectangleF rect, string? path, long size, string name)> tileHitTest = new List<(RectangleF, string?, long, string)>();
        private ToolTip hoverTip = new ToolTip();
        private Image? diskIcon;
        private string? lastHoverPath = null;
        private string? selectedPath = null;
        private System.Windows.Forms.Timer hoverTimer;
        private string? pendingHoverPath = null;
        private Point pendingHoverPoint;
        private Point lastMousePos;
        private ProgressBar marqueeSpinner;
        private volatile bool isResizing = false;
        private enum ColorScheme { ByPath, BySize, Monochrome, Pastel }
        private enum ByPathPalette { Rainbow, Grayscale, Warm, Cool, Pastel }
        private ColorScheme currentColorScheme = ColorScheme.ByPath;
        private ByPathPalette currentByPathPalette = ByPathPalette.Rainbow;
        private ToolStripMenuItem csByPathItem, csBySizeItem, csMonoItem, csPastelItem;
        private ToolStripMenuItem bpRainbowItem, bpGrayItem, bpWarmItem, bpCoolItem, bpPastelItem;

        public MainForm()
        {
            Text = "Simple SpaceMonger";
            Width = 1000;
            Height = 700;

            //btnBrowse = new Button { Text = "Browse...", Dock = DockStyle.Top, Height = 30 };
            //btnBrowse.Click += BtnBrowse_Click;

            //progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 20, Minimum = 0, Maximum = 100, Value = 0 };
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

            // Initialize folder dialog and root path before building menu so menu handlers can reference rootPath
            folderDialog = new FolderBrowserDialog();
            // Start in drives overview mode so user can pick a disk quickly
            rootPath = "__DRIVES__";

            // Try to load a disk icon image from the application folder (disk.webp)
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "disk.webp");
                if (File.Exists(iconPath)) diskIcon = Image.FromFile(iconPath);
            }
            catch { diskIcon = null; }

            // Marquee spinner overlay (hidden by default)
            marqueeSpinner = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Width = 140, Height = 18, Visible = false };
            // Will be added to drawPanel so it overlays the treemap
            // position will be set on resize
            // Note: add after drawPanel is created

            // Create menu strip to replace the old Browse button
            menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            var browseItem = new ToolStripMenuItem("Browse...", null, BtnBrowse_Click);
            var refreshItem = new ToolStripMenuItem("Refresh", null, (s, a) => { if (rootPath == "__DRIVES__") { FileScanner.ClearCache(); drawPanel.Invalidate(); } else { FileScanner.ClearCache(rootPath); _ = ScanAndInvalidateAsync(rootPath); } });
            var exitItem = new ToolStripMenuItem("Exit", null, (s, a) => { this.Close(); });
            fileMenu.DropDownItems.Add(browseItem);
            fileMenu.DropDownItems.Add(refreshItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitItem);

            var viewMenu = new ToolStripMenuItem("View");
            var zoomInMenu = new ToolStripMenuItem("Zoom In", null, (s, a) => ZoomInMenu_Click(s, a));
            var zoomOutMenu = new ToolStripMenuItem("Zoom Out", null, (s, a) => ZoomOutMenu_Click(s, a));
            viewMenu.DropDownItems.Add(zoomInMenu);
            viewMenu.DropDownItems.Add(zoomOutMenu);

            // Color scheme submenu
            var colorMenu = new ToolStripMenuItem("Color Scheme");
            csByPathItem = new ToolStripMenuItem("By Path", null, (s, a) => SetColorScheme(ColorScheme.ByPath)) { Checked = true };
            csBySizeItem = new ToolStripMenuItem("By Size", null, (s, a) => SetColorScheme(ColorScheme.BySize));
            csMonoItem = new ToolStripMenuItem("Monochrome", null, (s, a) => SetColorScheme(ColorScheme.Monochrome));
            csPastelItem = new ToolStripMenuItem("Pastel", null, (s, a) => SetColorScheme(ColorScheme.Pastel));
            colorMenu.DropDownItems.Add(csByPathItem);
            colorMenu.DropDownItems.Add(csBySizeItem);
            colorMenu.DropDownItems.Add(csMonoItem);
            colorMenu.DropDownItems.Add(csPastelItem);
            // By-Path palettes submenu
            var bpMenu = new ToolStripMenuItem("By-Path Palettes");
            bpRainbowItem = new ToolStripMenuItem("Rainbow", null, (s, a) => SetByPathPalette(ByPathPalette.Rainbow)) { Checked = true };
            bpGrayItem = new ToolStripMenuItem("Grayscale", null, (s, a) => SetByPathPalette(ByPathPalette.Grayscale));
            bpWarmItem = new ToolStripMenuItem("Warm", null, (s, a) => SetByPathPalette(ByPathPalette.Warm));
            bpCoolItem = new ToolStripMenuItem("Cool", null, (s, a) => SetByPathPalette(ByPathPalette.Cool));
            bpPastelItem = new ToolStripMenuItem("Pastel", null, (s, a) => SetByPathPalette(ByPathPalette.Pastel));
            bpMenu.DropDownItems.Add(bpRainbowItem);
            bpMenu.DropDownItems.Add(bpGrayItem);
            bpMenu.DropDownItems.Add(bpWarmItem);
            bpMenu.DropDownItems.Add(bpCoolItem);
            bpMenu.DropDownItems.Add(bpPastelItem);
            colorMenu.DropDownItems.Add(new ToolStripSeparator());
            colorMenu.DropDownItems.Add(bpMenu);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(colorMenu);

            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("About", null, (s, a) => AboutMenu_Click(s, a)));

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(viewMenu);
            menuStrip.Items.Add(helpMenu);

            // Add menuStrip last so it docks to the top above other top-docked controls
            Controls.Add(menuStrip);

            // Add marquee spinner to the drawPanel overlay and handle resizing to keep it centered
            drawPanel.Controls.Add(marqueeSpinner);
            drawPanel.Resize += (s, a) =>
            {
                marqueeSpinner.Location = new Point(Math.Max(4, (drawPanel.ClientSize.Width - marqueeSpinner.Width) / 2), Math.Max(4, (drawPanel.ClientSize.Height - marqueeSpinner.Height) / 2));
            };
            // Use ResizeBegin/ResizeEnd to avoid expensive painting while resizing
            this.ResizeBegin += (s, a) => { isResizing = true; marqueeSpinner.Visible = true; marqueeSpinner.BringToFront(); };
            this.ResizeEnd += (s, a) => { isResizing = false; marqueeSpinner.Visible = false; layoutStale = true; _ = System.Threading.Tasks.Task.Run(() => { RebuildLayoutAndBitmap(); this.BeginInvoke((Action)(() => drawPanel.Invalidate())); }); };

            // Initial async scan
            if (rootPath == "__DRIVES__")
                drawPanel.Invalidate();
            else
                _ = ScanAndInvalidateAsync(rootPath);
        }

        private async Task ScanAndInvalidateAsync(string path)
        {
            sizes.Clear();
            total = 0;
            progressBar.Value = 0;
            lblStatus.Text = "Enumerating directories...";
            drawPanel.Invalidate();

            // show marquee spinner while scanning
            marqueeSpinner.Visible = true;
            marqueeSpinner.BringToFront();

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
            finally
            {
                // hide spinner when finished
                marqueeSpinner.Visible = false;
            }

            // Rebuild layout and bitmap in background then invalidate to show cached image
            layoutStale = true;
            _ = System.Threading.Tasks.Task.Run(() => { RebuildLayoutAndBitmap(); this.BeginInvoke((Action)(() => drawPanel.Invalidate())); });
        }

        // Note: drawing, menu and interaction methods were moved into partial files:
        // - MainForm.Drawing.cs
        // - MainForm.Menu.cs
        // - MainForm.Interaction.cs
        // This file now contains only core initialization and shared fields.

    }
}
