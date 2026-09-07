using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.Helper;
using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1
    {
        private TabPage? tabPage其他项目;
        private DataGridView? dataGridView_other;
        private readonly BindingList<OtherProjectItem> _otherItems = new BindingList<OtherProjectItem>();
        private OtherProjectRepository? _otherRepo;
        private bool _otherItemsInitialized;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            EnsureOtherItemsInitialized();
        }

        private void EnsureOtherItemsInitialized()
        {
            if (_otherItemsInitialized || IsDisposed)
                return;
            _otherItemsInitialized = true;

            try
            {
                _otherRepo = new OtherProjectRepository(AppConfig.UserDbConn);
                EnsureOtherItemsTab();
                BindOtherItemsGrid();
                LoadOtherItems();
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "初始化其他项目失败");
            }
        }

        private void EnsureOtherItemsTab()
        {
            if (tabControl1.TabPages.Cast<TabPage>().Any(t => t.Name == "tabPage其他项目"))
                return;

            tabPage其他项目 = new TabPage
            {
                Name = "tabPage其他项目",
                Text = "其他项目",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };

            dataGridView_other = new DataGridView
            {
                Dock = DockStyle.Fill,
                Name = "dataGridView_other",
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            tabPage其他项目.Controls.Add(dataGridView_other);

            var insertAt = tabControl1.TabPages.Count;
            for (int i = 0; i < tabControl1.TabPages.Count; i++)
            {
                if (tabControl1.TabPages[i].Name == "tabPage措施")
                {
                    insertAt = i + 1;
                    break;
                }
            }
            tabControl1.TabPages.Insert(insertAt, tabPage其他项目);
        }

        private void BindOtherItemsGrid()
        {
            if (dataGridView_other == null) return;
            if (dataGridView_other.DataSource != null) return;

            GridManager.BindOnce(dataGridView_other, _otherItems, new List<ColumnConfig>
            {
                new() { FieldName = "名称", HeaderText = "名称", Width = 180, ReadOnly = false },
                new() { FieldName = "金额", HeaderText = "金额", Width = 120, ReadOnly = false,
                        Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight },
            });
            dataGridView_other.CellBeginEdit += dataGridView_other_CellBeginEdit;
            dataGridView_other.CellEndEdit += dataGridView_other_CellEndEdit;

            var menu = new ContextMenuStrip { Font = UiTheme.Font };
            var add = new ToolStripMenuItem("新增条目");
            add.Click += (_, _) => AddOtherItem();
            var del = new ToolStripMenuItem("删除条目");
            del.Click += (_, _) => DeleteOtherItem();
            menu.Items.Add(add);
            menu.Items.Add(del);
            UiTheme.ApplyToolStrip(menu);
            dataGridView_other.ContextMenuStrip = menu;
        }

        private void LoadOtherItems()
        {
            if (_otherRepo == null) return;
            try
            {
                var list = _otherRepo.LoadOrSeed(CurrentUnitProjectCode);
                _otherItems.RaiseListChangedEvents = false;
                try
                {
                    _otherItems.Clear();
                    foreach (var item in list)
                        _otherItems.Add(item);
                }
                finally
                {
                    _otherItems.RaiseListChangedEvents = true;
                    _otherItems.ResetBindings();
                }
                foreach (var item in list)
                    SyncOtherItemToMemory(item);
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "加载其他项目失败");
            }
        }

        private void AddOtherItem()
        {
            if (_otherRepo == null) return;
            try
            {
                var item = _otherRepo.AddCustom("新增项目", 0, CurrentUnitProjectCode);
                _otherItems.Add(item);
                SyncOtherItemToMemory(item);
                RefreshSummaryForCurrentUnit();
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "新增其他项目失败");
            }
        }

        private void DeleteOtherItem()
        {
            if (_otherRepo == null || dataGridView_other?.CurrentRow == null) return;
            int idx = dataGridView_other.CurrentRow.Index;
            if (idx < 0 || idx >= _otherItems.Count) return;
            var item = _otherItems[idx];
            if (OtherProjectRepository.IsZgjCode(item.清单编码) || item.名称 == "暂估价")
            {
                ErrorHandler.ShowBusiness("「暂估价」不可删除。");
                return;
            }

            var confirm = MessageBox.Show(this,
                $"确定删除「{item.名称}」吗？",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                if (_otherRepo.Delete(item.清单编码))
                {
                    _otherItems.RemoveAt(idx);
                    var mem = _allQingdan.FirstOrDefault(q => q.清单编码 == item.清单编码);
                    if (mem != null) _allQingdan.Remove(mem);
                    RefreshSummaryForCurrentUnit();
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "删除其他项目失败");
            }
        }

        private void dataGridView_other_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _otherItems.Count) return;
            var item = _otherItems[e.RowIndex];
            var col = dataGridView_other?.Columns[e.ColumnIndex].Name;
            if (col == "金额" && !item.IsAmountEditable)
                e.Cancel = true;
            if (col == "名称" && !item.IsNameEditable)
                e.Cancel = true;
        }

        private void dataGridView_other_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_otherRepo == null || e.RowIndex < 0 || e.RowIndex >= _otherItems.Count) return;
            var item = _otherItems[e.RowIndex];
            var col = dataGridView_other?.Columns[e.ColumnIndex].Name;
            try
            {
                if (col == "金额")
                {
                    if (!item.IsAmountEditable)
                    {
                        item.金额 = 0;
                        return;
                    }
                    _otherRepo.SaveAmount(item.清单编码, item.金额, CurrentUnitProjectCode);
                    SyncOtherItemToMemory(item);
                    RefreshSummaryForCurrentUnit();
                }
                else if (col == "名称")
                {
                    if (!item.IsNameEditable) return;
                    _otherRepo.SaveName(item.清单编码, item.名称);
                    SyncOtherItemToMemory(item);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "保存其他项目失败");
            }
        }

        private void SyncOtherItemToMemory(OtherProjectItem item)
        {
            var qd = _allQingdan.FirstOrDefault(q => q.清单编码 == item.清单编码);
            if (qd == null)
            {
                qd = new Qingdan
                {
                    清单编码 = item.清单编码,
                    清单名称 = item.名称,
                    项目类别 = QingdanCategory.其他项目,
                    综合合价 = item.金额,
                    综合单价 = 0,
                    工程量 = 0,
                    单位工程编码 = CurrentUnitProjectCode
                };
                _allQingdan.Add(qd);
            }
            else
            {
                qd.清单名称 = item.名称;
                qd.综合合价 = item.金额;
                qd.项目类别 = QingdanCategory.其他项目;
                qd.单位工程编码 = CurrentUnitProjectCode;
            }
        }
    }
}
