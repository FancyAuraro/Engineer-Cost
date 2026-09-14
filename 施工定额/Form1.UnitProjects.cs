using 施工定额.Entity;
using 施工定额.Helper;
using 施工定额.Service;
using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1
    {
        private UnitProjectController? _unitController;
        private string _currentUnitProjectCode = UnitProject.DefaultCode;

        public string CurrentUnitProjectCode =>
            string.IsNullOrEmpty(_currentUnitProjectCode) ? UnitProject.DefaultCode : _currentUnitProjectCode;

        private void EnsureUnitProjectsInitialized()
        {
            _unitController ??= AppComposition.CreateUnitProjectController();
            try
            {
                _unitController.EnsureDefault();
                WireUnitProjectTree();
                ReloadUnitProjectTree(CurrentUnitProjectCode);
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "初始化单位工程失败"); }
        }

        private void WireUnitProjectTree()
        {
            treeProject.AfterSelect -= treeProject_AfterSelect;
            treeProject.AfterSelect += treeProject_AfterSelect;
            var menu = new ContextMenuStrip { Font = UiTheme.Font };
            var add = new ToolStripMenuItem("新建单位工程"); add.Click += (_, _) => AddUnitProject();
            var rename = new ToolStripMenuItem("重命名"); rename.Click += (_, _) => RenameUnitProject();
            var del = new ToolStripMenuItem("删除单位工程"); del.Click += (_, _) => DeleteUnitProject();
            menu.Items.Add(add); menu.Items.Add(rename); menu.Items.Add(del);
            UiTheme.ApplyToolStrip(menu);
            treeProject.ContextMenuStrip = menu;
        }

        private void ReloadUnitProjectTree(string? selectCode = null)
        {
            if (_unitController == null) return;
            var list = _unitController.ListAll();
            treeProject.BeginUpdate();
            try
            {
                treeProject.Nodes.Clear();
                var root = new TreeNode("工程结构") { Name = "root" };
                TreeNode? toSelect = null;
                foreach (var u in list)
                {
                    var node = new TreeNode(u.名称) { Name = u.编码, Tag = u.编码 };
                    root.Nodes.Add(node);
                    if (selectCode != null && u.编码 == selectCode) toSelect = node;
                }
                treeProject.Nodes.Add(root);
                root.Expand();
                if (toSelect != null) treeProject.SelectedNode = toSelect;
                else if (root.Nodes.Count > 0) treeProject.SelectedNode = root.Nodes[0];
            }
            finally { treeProject.EndUpdate(); }
        }

        private void treeProject_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is not string code || string.IsNullOrEmpty(code) || code == _currentUnitProjectCode) return;
            _currentUnitProjectCode = code;
            RebuildCategoryViews();
            _selection.SelectQingdan("");
            UpdateDisplay(DisplayType.Dinge);
            UpdateDisplay(DisplayType.Xiaohaoliang);
            LoadOtherItems();
            RefreshSummaryForCurrentUnit();
        }

        private void RefreshSummaryForCurrentUnit()
        {
            if (tabControl1.SelectedIndex < 0) return;
            string name = tabControl1.TabPages[tabControl1.SelectedIndex].Name;
            if (name == "tabRenCaiJi") dataGridView3.DataSource = _summaryPresenter.GetRenCaiJiSummaryFromMemory("", CurrentUnitProjectCode);
            else if (name == "tabCostSummary") dataGridView4.DataSource = _summaryPresenter.GetCostSummaryData(CurrentUnitProjectCode);
        }

        private void AddUnitProject()
        {
            if (_unitController == null) return;
            string? name = PromptText("新建单位工程", "请输入单位工程名称：", "新单位工程");
            if (name == null) return;
            try
            {
                var u = _unitController.Add(name);
                _currentUnitProjectCode = u.编码;
                ReloadUnitProjectTree(u.编码);
                RebuildCategoryViews();
                LoadOtherItems();
                RefreshSummaryForCurrentUnit();
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "新建单位工程失败"); }
        }

        private void RenameUnitProject()
        {
            if (_unitController == null) return;
            var node = treeProject.SelectedNode;
            if (node?.Tag is not string code || string.IsNullOrEmpty(code))
            {
                ErrorHandler.ShowBusiness("请先选中一个单位工程。");
                return;
            }
            string? name = PromptText("重命名单位工程", "请输入新名称：", node.Text);
            if (name == null) return;
            try
            {
                _unitController.Rename(code, name);
                ReloadUnitProjectTree(code);
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "重命名失败"); }
        }

        private void DeleteUnitProject()
        {
            if (_unitController == null) return;
            var node = treeProject.SelectedNode;
            if (node?.Tag is not string code || string.IsNullOrEmpty(code))
            {
                ErrorHandler.ShowBusiness("请先选中一个单位工程。");
                return;
            }
            var confirm = MessageBox.Show(this,
                $"确定删除单位工程「{node.Text}」吗？\n（其下不能有分部分项/措施清单；其他项目会一并清除）",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            try
            {
                _unitController.Delete(code);
                _currentUnitProjectCode = UnitProject.DefaultCode;
                ReloadUnitProjectTree(UnitProject.DefaultCode);
                RebuildCategoryViews();
                LoadOtherItems();
                RefreshSummaryForCurrentUnit();
            }
            catch (Exception ex) { ErrorHandler.Show(ex, "删除单位工程失败"); }
        }

        private static string? PromptText(string title, string label, string defaultValue)
        {
            using var form = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, 120),
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false
            };
            var lbl = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };
            var tb = new TextBox { Left = 12, Top = 36, Width = 330, Text = defaultValue };
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 180, Top = 75, Width = 75 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 265, Top = 75, Width = 75 };
            form.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
            form.AcceptButton = ok; form.CancelButton = cancel;
            UiTheme.ApplyTo(form);
            if (form.ShowDialog() != DialogResult.OK) return null;
            var text = tb.Text.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string NormalizeUnitCode(string? code) =>
            string.IsNullOrWhiteSpace(code) ? UnitProject.DefaultCode : code.Trim();

        private bool MatchesCurrentUnit(Qingdan qd) => NormalizeUnitCode(qd.单位工程编码) == CurrentUnitProjectCode;
    }
}
