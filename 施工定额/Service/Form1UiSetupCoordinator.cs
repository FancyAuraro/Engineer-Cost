using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.UI;

namespace 施工定额.Service
{
    /// <summary>
    /// 负责主窗体的控件初始化与数据网格绑定。
    /// Form1 只负责组装依赖和响应生命周期事件。
    /// </summary>
    public sealed class Form1UiSetupCoordinator
    {
        private readonly TabControl _tabControl;
        private readonly DataGridView _qingdanGrid;
        private readonly DataGridView _dingeGrid;
        private readonly DataGridView _xiaohaoliangGrid;
        private readonly BindingList<Qingdan> _qingdanList;
        private readonly BindingList<Qingdan> _measureQingdanList;
        private readonly BindingList<Dinge> _dingeList;
        private readonly BindingList<Dinge> _measureDingeList;
        private readonly BindingList<Xiaohaoliang> _xiaohaoliangList;
        private readonly ContextMenuBuilder _menuBuilder;
        private readonly DataGridViewCellEventHandler _measureQingdanClick;
        private readonly DataGridViewCellEventHandler _measureQingdanDoubleClick;
        private readonly DataGridViewCellEventHandler _measureQingdanEndEdit;
        private readonly DataGridViewCellEventHandler _measureDingeClick;
        private readonly DataGridViewCellEventHandler _measureDingeChanged;

        public FeatureDataGridView? MeasureQingdanGrid { get; private set; }
        public DataGridView? MeasureDingeGrid { get; private set; }

        public Form1UiSetupCoordinator(
            TabControl tabControl,
            DataGridView qingdanGrid,
            DataGridView dingeGrid,
            DataGridView xiaohaoliangGrid,
            BindingList<Qingdan> qingdanList,
            BindingList<Qingdan> measureQingdanList,
            BindingList<Dinge> dingeList,
            BindingList<Dinge> measureDingeList,
            BindingList<Xiaohaoliang> xiaohaoliangList,
            ContextMenuBuilder menuBuilder,
            DataGridViewCellEventHandler measureQingdanClick,
            DataGridViewCellEventHandler measureQingdanDoubleClick,
            DataGridViewCellEventHandler measureQingdanEndEdit,
            DataGridViewCellEventHandler measureDingeClick,
            DataGridViewCellEventHandler measureDingeChanged)
        {
            _tabControl = tabControl;
            _qingdanGrid = qingdanGrid;
            _dingeGrid = dingeGrid;
            _xiaohaoliangGrid = xiaohaoliangGrid;
            _qingdanList = qingdanList;
            _measureQingdanList = measureQingdanList;
            _dingeList = dingeList;
            _measureDingeList = measureDingeList;
            _xiaohaoliangList = xiaohaoliangList;
            _menuBuilder = menuBuilder;
            _measureQingdanClick = measureQingdanClick;
            _measureQingdanDoubleClick = measureQingdanDoubleClick;
            _measureQingdanEndEdit = measureQingdanEndEdit;
            _measureDingeClick = measureDingeClick;
            _measureDingeChanged = measureDingeChanged;
        }

        public void EnsureMeasureTab()
        {
            var existing = _tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == "tabPage措施");
            if (existing != null)
            {
                MeasureQingdanGrid = existing.Controls.OfType<SplitContainer>().FirstOrDefault()?.Panel1.Controls.OfType<FeatureDataGridView>().FirstOrDefault();
                MeasureDingeGrid = existing.Controls.OfType<SplitContainer>().FirstOrDefault()?.Panel2.Controls.OfType<DataGridView>().FirstOrDefault();
                return;
            }

            var tab = new TabPage
            {
                Name = "tabPage措施",
                Text = "措施项目",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };
            var split = new SplitContainer
            {
                Name = "measureSplit",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            var measureQingdan = new FeatureDataGridView
            {
                Dock = DockStyle.Fill,
                Name = "dataGridView_measure",
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            var measureDinge = new DataGridView
            {
                Dock = DockStyle.Fill,
                Name = "DataGridView_measure_dinge",
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            measureQingdan.CellClick += _measureQingdanClick;
            measureQingdan.CellDoubleClick += _measureQingdanDoubleClick;
            measureQingdan.CellEndEdit += _measureQingdanEndEdit;
            measureDinge.CellClick += _measureDingeClick;
            measureDinge.CellValueChanged += _measureDingeChanged;

            split.Panel1.Controls.Add(measureQingdan);
            split.Panel2.Controls.Add(measureDinge);
            tab.Controls.Add(split);

            var insertAt = 1;
            for (int i = 0; i < _tabControl.TabPages.Count; i++)
            {
                if (_tabControl.TabPages[i].Name == "tabPage1")
                {
                    insertAt = i + 1;
                    break;
                }
            }
            _tabControl.TabPages.Insert(insertAt, tab);
            MeasureQingdanGrid = measureQingdan;
            MeasureDingeGrid = measureDinge;
        }

        public void InitializeGridColumns()
        {
            GridManager.BindOnce(_qingdanGrid, _qingdanList, GridColumns.Qingdan);
            if (!_qingdanGrid.Columns.Contains("btnViewImage"))
            {
                _qingdanGrid.Columns.Add(new DataGridViewButtonColumn
                {
                    Name = "btnViewImage", HeaderText = "图片", Text = "查看",
                    UseColumnTextForButtonValue = true, Width = 60
                });
            }
            GridLayoutStore.Attach(_qingdanGrid);
            GridManager.BindOnce(_dingeGrid, _dingeList, GridColumns.Dinge);
            GridManager.BindOnce(_xiaohaoliangGrid, _xiaohaoliangList, GridColumns.Xiaohaoliang);

            if (MeasureQingdanGrid != null)
            {
                GridManager.BindOnce(MeasureQingdanGrid, _measureQingdanList, GridColumns.MeasureQingdan);
                MeasureQingdanGrid.Tag = QingdanCategory.措施项目;
                MeasureQingdanGrid.ContextMenuStrip = _menuBuilder.BuildQingdanMenu(MeasureQingdanGrid, QingdanCategory.措施项目);
            }
            if (MeasureDingeGrid != null)
                GridManager.BindOnce(MeasureDingeGrid, _measureDingeList, GridColumns.Dinge);
        }
    }
}
