using System.ComponentModel;
using System.Diagnostics;
using 施工定额.Entity;
using 施工定额.Export;
using 施工定额.Helper;
using 施工定额.Service;
using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1
    {
        private void EnsureSettingsMenu()
        {
            foreach (ToolStripItem item in menuStrip1.Items)
            {
                if (item.Text == "设置") return;
            }
            var settingsMenu = new ToolStripMenuItem("设置");
            var feeItem = new ToolStripMenuItem("费率设置(&F)...");
            feeItem.Click += (_, _) => OpenFeeSettings();
            settingsMenu.DropDownItems.Add(feeItem);
            menuStrip1.Items.Add(settingsMenu);
        }

        private void EnsureHelpMenu()
        {
            foreach (ToolStripItem item in menuStrip1.Items)
            {
                if (item.Text is "帮助" or "帮助(&H)") return;
            }
            var help = new ToolStripMenuItem("帮助(&H)");
            var about = new ToolStripMenuItem("关于(&A)...");
            about.Click += (_, _) => ShowAbout();
            var openLog = new ToolStripMenuItem("打开日志目录");
            openLog.Click += (_, _) => OpenFolder(AppLogger.LogDirectory);
            var openData = new ToolStripMenuItem("打开数据目录");
            openData.Click += (_, _) => OpenFolder(AppConfig.DataDirectory);
            var restore = new ToolStripMenuItem("从备份恢复用户库...");
            restore.Click += (_, _) => RestoreUserDbFromBackup();
            help.DropDownItems.Add(about);
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add(openLog);
            help.DropDownItems.Add(openData);
            help.DropDownItems.Add(restore);
            menuStrip1.Items.Add(help);
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "打开目录失败"); }
        }

        private void ShowAbout()
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            ErrorHandler.ShowBusiness(
                $"施工定额（Engineer-Cost）\n\n版本：{ver}\n用户数据：{AppConfig.DataDirectory}\n系统定额库：{AppConfig.SystemDbFilePath}\n\n个人学习项目。",
                "关于");
        }

        private void RestoreUserDbFromBackup()
        {
            var backups = UserDbBackup.ListBackups();
            if (backups.Count == 0)
            {
                ErrorHandler.ShowBusiness($"未找到备份文件。\n备份目录：{UserDbBackup.BackupDirectory}", "恢复备份");
                return;
            }
            using var ofd = new OpenFileDialog
            {
                Title = "选择用户库备份",
                InitialDirectory = UserDbBackup.BackupDirectory,
                Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            var confirm = MessageBox.Show(this,
                "恢复备份将覆盖当前用户库。\n恢复前会自动再备份一份当前库。\n\n是否继续？",
                "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            try
            {
                UserDbBackup.Restore(ofd.FileName, AppConfig.UserDbFilePath);
                ReloadAndRecalculateEverything();
                ErrorHandler.ShowBusiness("用户库已从备份恢复，并已重新加载数据。", "恢复备份");
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "恢复备份失败"); }
        }

        private void WirePlaceholderMenus()
        {
            打开ToolStripMenuItem.Click += (_, _) =>
                ErrorHandler.ShowBusiness("「打开工程」尚未实现，当前工程数据保存在用户库中。", "提示");
            保存ToolStripMenuItem.Click += (_, _) =>
                ErrorHandler.ShowBusiness("数据已在编辑时自动保存到用户库，无需手动保存。", "提示");
        }

        private void OpenFeeSettings()
        {
            using var form = new FeeSettingsForm(AppConfig.FeeRates);
            if (form.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                AppConfig.SaveUserFeeRates(form.ResultRates);
                _calcService = AppComposition.CreateCostCalculationService(form.ResultRates);
                _qingdanPresenter.ReplaceCalcService(_calcService);
                _summaryPresenter.ReplaceCalcService(_calcService);
                ErrorHandler.ShowBusiness("费率已保存，并已按新费率重算全部清单。", "费率设置");
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "保存费率失败"); }
        }

        private void toolStripButton2_Click(object sender, EventArgs e) =>
            ExportCoordinator.ExportYdjc(this, _allQingdan, _calcService);

        private async void toolStripButton1_Click(object sender, EventArgs e)
        {
            var btn = sender as ToolStripItem;
            try
            {
                if (btn != null) btn.Enabled = false;
                Cursor = Cursors.WaitCursor;
                await UpdateCoordinator.CheckAllAsync(this, silentIfUpToDate: false);
            }
            finally
            {
                Cursor = Cursors.Default;
                if (btn != null) btn.Enabled = true;
            }
        }

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
