using System.ComponentModel;
using System.Diagnostics;
using 施工定额.Entity;
using 施工定额.Export;
using 施工定额.Helper;
using 施工定额.UI;

namespace 施工定额.Service
{
    /// <summary>
    /// 负责主窗体菜单命令的业务编排。
    /// Form1 仅负责生命周期和菜单入口，具体设置、备份、导出与更新流程集中在这里。
    /// </summary>
    public sealed class Form1MenuCoordinator
    {
        private readonly Form _owner;
        private readonly MenuStrip _menuStrip;
        private readonly ToolStrip _toolStrip;
        private readonly BindingList<Qingdan> _allQingdan;
        private readonly QingdanPresenter _qingdanPresenter;
        private readonly SummaryPresenter _summaryPresenter;
        private readonly Action _reloadAndRecalculate;
        private readonly Func<string> _currentUnitProjectCode;
        private readonly Func<ICostCalculationService> _getCalcService;
        private readonly Action<ICostCalculationService> _replaceCalcService;

        public Form1MenuCoordinator(
            Form owner,
            MenuStrip menuStrip,
            ToolStrip toolStrip,
            BindingList<Qingdan> allQingdan,
            QingdanPresenter qingdanPresenter,
            SummaryPresenter summaryPresenter,
            Action reloadAndRecalculate,
            Func<string> currentUnitProjectCode,
            Func<ICostCalculationService> getCalcService,
            Action<ICostCalculationService> replaceCalcService)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _menuStrip = menuStrip ?? throw new ArgumentNullException(nameof(menuStrip));
            _toolStrip = toolStrip ?? throw new ArgumentNullException(nameof(toolStrip));
            _allQingdan = allQingdan ?? throw new ArgumentNullException(nameof(allQingdan));
            _qingdanPresenter = qingdanPresenter ?? throw new ArgumentNullException(nameof(qingdanPresenter));
            _summaryPresenter = summaryPresenter ?? throw new ArgumentNullException(nameof(summaryPresenter));
            _reloadAndRecalculate = reloadAndRecalculate ?? throw new ArgumentNullException(nameof(reloadAndRecalculate));
            _currentUnitProjectCode = currentUnitProjectCode ?? throw new ArgumentNullException(nameof(currentUnitProjectCode));
            _getCalcService = getCalcService ?? throw new ArgumentNullException(nameof(getCalcService));
            _replaceCalcService = replaceCalcService ?? throw new ArgumentNullException(nameof(replaceCalcService));
        }

        public void EnsureMenus()
        {
            EnsureSettingsMenu();
            EnsureHelpMenu();
            WirePlaceholderMenus();
        }

        public void ExportYdjc() =>
            ExportCoordinator.ExportYdjc(_owner, _allQingdan, _getCalcService());

        public async Task CheckForUpdatesAsync(ToolStripItem? sourceButton)
        {
            try
            {
                if (sourceButton != null) sourceButton.Enabled = false;
                _owner.Cursor = Cursors.WaitCursor;
                await UpdateCoordinator.CheckAllAsync(_owner, silentIfUpToDate: false);
            }
            finally
            {
                _owner.Cursor = Cursors.Default;
                if (sourceButton != null) sourceButton.Enabled = true;
            }
        }

        private void EnsureSettingsMenu()
        {
            foreach (ToolStripItem item in _menuStrip.Items)
            {
                if (item.Text == "设置") return;
            }

            var settingsMenu = new ToolStripMenuItem("设置");
            var feeItem = new ToolStripMenuItem("费率设置(&F)...");
            feeItem.Click += (_, _) => OpenFeeSettings();
            settingsMenu.DropDownItems.Add(feeItem);
            _menuStrip.Items.Add(settingsMenu);
        }

        private void EnsureHelpMenu()
        {
            foreach (ToolStripItem item in _menuStrip.Items)
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
            _menuStrip.Items.Add(help);
        }

        private void OpenFolder(string path)
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
            if (ofd.ShowDialog(_owner) != DialogResult.OK) return;

            var confirm = MessageBox.Show(_owner,
                "恢复备份将覆盖当前用户库。\n恢复前会自动再备份一份当前库。\n\n是否继续？",
                "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                UserDbBackup.Restore(ofd.FileName, AppConfig.UserDbFilePath);
                _reloadAndRecalculate();
                ErrorHandler.ShowBusiness("用户库已从备份恢复，并已重新加载数据。", "恢复备份");
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "恢复备份失败"); }
        }

        private void WirePlaceholderMenus()
        {
            var open = FindMenuItem("打开");
            if (open != null)
                open.Click += (_, _) => ErrorHandler.ShowBusiness("「打开工程」尚未实现，当前工程数据保存在用户库中。", "提示");

            var save = FindMenuItem("保存");
            if (save != null)
                save.Click += (_, _) => ErrorHandler.ShowBusiness("数据已在编辑时自动保存到用户库，无需手动保存。", "提示");
        }

        private ToolStripMenuItem? FindMenuItem(string text) =>
            _menuStrip.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text == text);

        private void OpenFeeSettings()
        {
            using var form = new FeeSettingsForm(AppConfig.FeeRates);
            if (form.ShowDialog(_owner) != DialogResult.OK) return;

            try
            {
                AppConfig.SaveUserFeeRates(form.ResultRates);
                var calcService = AppComposition.CreateCostCalculationService(form.ResultRates);
                _replaceCalcService(calcService);
                _qingdanPresenter.ReplaceCalcService(calcService);
                _summaryPresenter.ReplaceCalcService(calcService);
                ErrorHandler.ShowBusiness("费率已保存，并已按新费率重算全部清单。", "费率设置");
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "保存费率失败"); }
        }
    }
}
