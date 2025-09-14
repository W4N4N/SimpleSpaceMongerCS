using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SimpleSpaceMongerCS
{
    public partial class MainForm
    {
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                rootPath = folderDialog.SelectedPath;
                _ = ScanAndInvalidateAsync(rootPath);
            }
        }

        private void ZoomInMenu_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(lastHoverPath) && !lastHoverPath.EndsWith("|FREE|"))
            {
                if (Directory.Exists(lastHoverPath))
                {
                    rootPath = lastHoverPath;
                    _ = ScanAndInvalidateAsync(rootPath);
                }
                else
                {
                    MessageBox.Show("Selected item is not a folder to zoom into.", "Zoom In", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("No folder selected to zoom in. Hover or click a folder first.", "Zoom In", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ZoomOutMenu_Click(object? sender, EventArgs e)
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

        private void AboutMenu_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Simple SpaceMonger - simple disk treemap viewer", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetColorScheme(ColorScheme scheme)
        {
            currentColorScheme = scheme;
            csByPathItem.Checked = (scheme == ColorScheme.ByPath);
            csBySizeItem.Checked = (scheme == ColorScheme.BySize);
            csMonoItem.Checked = (scheme == ColorScheme.Monochrome);
            csPastelItem.Checked = (scheme == ColorScheme.Pastel);
            drawPanel.Invalidate();
        }

        private void SetByPathPalette(ByPathPalette p)
        {
            currentByPathPalette = p;
            bpRainbowItem.Checked = (p == ByPathPalette.Rainbow);
            bpGrayItem.Checked = (p == ByPathPalette.Grayscale);
            bpWarmItem.Checked = (p == ByPathPalette.Warm);
            bpCoolItem.Checked = (p == ByPathPalette.Cool);
            bpPastelItem.Checked = (p == ByPathPalette.Pastel);
            drawPanel.Invalidate();
        }
    }
}
