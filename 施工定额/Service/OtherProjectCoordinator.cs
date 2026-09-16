using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.Helper;
using 施工定额.UI;

namespace 施工定额.Service
{
    /// <summary>
    /// 负责“其他项目”页签的 UI 组装、编辑持久化与内存同步。
    /// Form1 仅保留生命周期入口和单位工程切换入口。
    /// </summary>
    public sealed class OtherProjectCoordinator
    {
        private readonly Form _owner;
        private readonly TabControl _tabControl;
        private readonly BindingList<Qingdan> _allQingdan;
        private readonly BindingList<OtherProjectItem> _items = new();
        private readonly Func<string> _currentUnitProjectCode;
        private readonly Action _refreshSummary;
        private readonly OtherProjectRepository _repo;

        private TabPage? _tab;
        private DataGridView? _grid;
        private bool _initialized;

        public OtherProjectCoordinator(
            Form owner,
            TabControl tabControl,
            BindingList<Qingdan> allQingdan,
            Func<string> currentUnitProjectCode,
            Action refreshSummary,
            OtherProjectRepository repo)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _allQingdan = allQingdan ?? throw new ArgumentNullException(nameof(allQingdan));
            _currentUnitProjectCode = currentUnitProjectCode ?? throw new ArgumentNullException(nameof(currentUnitProjectCode));
            _refreshSummary = refreshSummary ?? throw new ArgumentNullException(nameof(refreshSummary));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public void EnsureInitialized()
        {
            if (_initialized || _owner.IsDisposed)
                return;
            _initialized = true;

            try
            {
                EnsureTab();
                BindGrid();
                Load();
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "初始化其他项目失败");
            }
        }

        public void Load()
        {
            if (!_initialized || _grid == null)
                return;

            try
            {
                var list = _repo.LoadOrSeed(_currentUnitProjectCode());
                _items.RaiseListChangedEvents = false;
                try
                {
                    _items.Clear();
                    foreach (var item in list)
                        _items.Add(item);
                }
                finally
                {
                    _items.RaiseListChangedEvents = true;
                    _items.ResetBindings();
                }

                foreach (var item in list)
                    SyncToMemory(item);
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "加载其他项目失败");
            }
        }

        private void EnsureTab()
        {
            var existing = _tabControl.TabPages.Cast<TabPage>()
                .FirstOrDefault(t => t.Name == "tabPage其他项目");
            if (existing != null)
            {
                _tab = existing;
                _grid = existing.Controls.OfType<DataGridView>().FirstOrDefault();
                return;
            }

            _tab = new TabPage
            {
                Name = "tabPage其他项目",
                Text = "其他项目",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Name = "dataGridView_other",
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            _tab.Controls.Add(_grid);

            var insertAt = _tabControl.TabPages.Count;
            for (int i = 0; i < _tabControl.TabPages.Count; i++)
            {
                if (_tabControl.TabPages[i].Name == "tabPage措施")
                {
                    insertAt = i + 1;
                    break;
                }
            }
            _tabControl.TabPages.Insert(insertAt, _tab);
        }

        private void BindGrid()
        {
            if (_grid == null || _grid.DataSource != null)
                return;

            GridManager.BindOnce(_grid, _items, new List<ColumnConfig>
            {
                new() { FieldName = "名称", HeaderText = "名称", Width = 180, ReadOnly = false },
                new() { FieldName = "金额", HeaderText = "金额", Width = 120, ReadOnly = false,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
            });
            _grid.CellBeginEdit += OnCellBeginEdit;
            _grid.CellEndEdit += OnCellEndEdit;

            var menu = new ContextMenuStrip { Font = UiTheme.Font };
            var add = new ToolStripMenuItem("新增条目");
            add.Click += (_, _) => AddItem();
            var del = new ToolStripMenuItem("删除条目");
            del.Click += (_, _) => DeleteItem();
            menu.Items.Add(add);
            menu.Items.Add(del);
            UiTheme.ApplyToolStrip(menu);
            _grid.ContextMenuStrip = menu;
        }

        private void AddItem()
        {
            try
            {
                var item = _repo.AddCustom("新增项目", 0, _currentUnitProjectCode());
                _items.Add(item);
                SyncToMemory(item);
                _refreshSummary();
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "新增其他项目失败");
            }
        }

        private void DeleteItem()
        {
            if (_grid?.CurrentRow == null)
                return;

            int index = _grid.CurrentRow.Index;
            if (index < 0 || index >= _items.Count)
                return;

            var item = _items[index];
            if (OtherProjectRepository.IsZgjCode(item.清单编码) || item.名称 == "暂估价")
            {
                ErrorHandler.ShowBusiness("「暂估价」不可删除。");
                return;
            }

            var confirm = MessageBox.Show(_owner,
                $"确定删除「{item.名称}」吗？",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                if (!_repo.Delete(item.清单编码))
                    return;

                _items.RemoveAt(index);
                var memory = _allQingdan.FirstOrDefault(q => q.清单编码 == item.清单编码);
                if (memory != null)
                    _allQingdan.Remove(memory);
                _refreshSummary();
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "删除其他项目失败");
            }
        }

        private void OnCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.RowIndex >= _items.Count || e.ColumnIndex < 0)
                return;

            var item = _items[e.RowIndex];
            var column = _grid.Columns[e.ColumnIndex].Name;
            if (column == "金额" && !item.IsAmountEditable)
                e.Cancel = true;
            if (column == "名称" && !item.IsNameEditable)
                e.Cancel = true;
        }

        private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.RowIndex >= _items.Count || e.ColumnIndex < 0)
                return;

            var item = _items[e.RowIndex];
            var column = _grid.Columns[e.ColumnIndex].Name;
            try
            {
                if (column == "金额")
                {
                    if (!item.IsAmountEditable)
                    {
                        item.金额 = 0;
                        return;
                    }
                    _repo.SaveAmount(item.清单编码, item.金额, _currentUnitProjectCode());
                    SyncToMemory(item);
                    _refreshSummary();
                }
                else if (column == "名称")
                {
                    if (!item.IsNameEditable)
                        return;
                    _repo.SaveName(item.清单编码, item.名称);
                    SyncToMemory(item);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex, "保存其他项目失败");
            }
        }

        private void SyncToMemory(OtherProjectItem item)
        {
            var qd = _allQingdan.FirstOrDefault(q => q.清单编码 == item.清单编码);
            if (qd == null)
            {
                _allQingdan.Add(new Qingdan
                {
                    清单编码 = item.清单编码,
                    清单名称 = item.名称,
                    项目类别 = QingdanCategory.其他项目,
                    综合合价 = item.金额,
                    综合单价 = 0,
                    工程量 = 0,
                    单位工程编码 = _currentUnitProjectCode()
                });
                return;
            }

            qd.清单名称 = item.名称;
            qd.综合合价 = item.金额;
            qd.项目类别 = QingdanCategory.其他项目;
            qd.单位工程编码 = _currentUnitProjectCode();
        }
    }
}
