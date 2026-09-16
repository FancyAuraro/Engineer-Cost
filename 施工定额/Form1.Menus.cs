using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1
    {
        private void EnsureSettingsMenu() => _menuCoordinator.EnsureMenus();

        private void EnsureHelpMenu() { }

        private void WirePlaceholderMenus() { }

        private void toolStripButton2_Click(object sender, EventArgs e) =>
            _menuCoordinator.ExportYdjc();

        private async void toolStripButton1_Click(object sender, EventArgs e) =>
            await _menuCoordinator.CheckForUpdatesAsync(sender as ToolStripItem);

        private void ApplyToolbarIcons()
        {
            toolStripButton1.Image = CreateRefreshIcon();
            toolStripButton1.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            toolStripButton1.TextImageRelation = TextImageRelation.ImageAboveText;
            toolStripButton2.Image = CreateExportIcon();
            toolStripButton2.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            toolStripButton2.TextImageRelation = TextImageRelation.ImageAboveText;
            toolStripButton2.Text = "导出";
        }

        private void ApplyUiFont()
        {
            UiTheme.ApplyTo(this);
            UiTheme.ApplyToolStrip(menuStrip1);
            UiTheme.ApplyToolStrip(toolStrip1);
        }

        private static Bitmap CreateRefreshIcon()
        {
            const int size = 32;
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.FromArgb(0, 120, 215), 2.5f);
            g.DrawArc(pen, new Rectangle(5, 5, size - 10, size - 10), 40, 280);
            using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillPolygon(brush, new[] { new PointF(size - 7, 8), new PointF(size - 14, 6), new PointF(size - 12, 14) });
            return bmp;
        }

        private static Bitmap CreateExportIcon()
        {
            const int size = 32;
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.FromArgb(16, 124, 16), 2f);
            using var brush = new SolidBrush(Color.FromArgb(16, 124, 16));
            g.DrawRectangle(pen, 8, 4, 16, 22);
            g.DrawLine(pen, 18, 4, 18, 10);
            g.DrawLine(pen, 18, 10, 24, 10);
            g.DrawLine(pen, 18, 4, 24, 10);
            g.DrawLine(pen, 16, 14, 16, 22);
            g.FillPolygon(brush, new[] { new PointF(16, 24), new PointF(12, 20), new PointF(20, 20) });
            return bmp;
        }
    }
}
