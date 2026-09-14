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
        private readonly Action<DataGridViewCellEventHandler> _wireMeasureQingdanClick;
        private readonly Action<DataGridViewCellEventHandler> _wireMeasureQingdanDoubleClick;
        private readonly Action<DataGridViewCellEventHandler> _wireMeasureQingdanEndEdit;
        private readonly Action<DataGridViewCellEventHandler> _wireMeasureDingeClick;
        private readonly Action<DataGridViewCellEventHandler> _wireMeasureDingeChanged;

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
            Action<DataGridViewCellEventHandler> wireMeasureQingdanClick,
            Action<DataGridViewCellEventHandler> wireMeasureQingdanDoubleClick,
            Action<DataGridViewCellEventHandler> wireMeasureQingdanEndEdit,
            Action<DataGridViewCellEventHandler> wireMeasureDingeClick,
            Action<DataGridViewCellEventHandler> wireMeasureDingeChanged)
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
            _wireMeasureQingdanClick = wireMeasureQingdanClick;
            _wireMeasureQingdanDoubleClick = wireMeasureQingdanDoubleClick;
            _wireMeasureQingdanEndEdit = wireMeasureQingdanEndEdit;
            _wireMeasureDingeClick = wireMeasureDingeClick;
            _wireMeasureDingeChanged = wireMeasureDingeChanged;
        }

        public void EnsureMeasureTab()
        {
            if (_tabControl.TabPages.Cast<TabPage>().Any(t => t.Name == "tabPage措施"))
            {
                MeasureQingdanGrid = _tabControl.Controls.OfType<FeatureDataGridView>().FirstOrDefault();
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

            measureQingdan.CellClick += (s, e) => _wireMeasureQingdanClick(h => { });
            measureQingdan.CellDoubleClick += (s, e) => _wireMeasureQingdanDoubleClick(h => { });
            measureQingdan.CellEndEdit += (s, e) => _wireMeasureQingdanEndEdit(h => { });
            measureDinge.CellClick += (s, e) => _wireMeasureDingeClick(h => { });
            measureDinge.CellValueChanged += (s, e) => _wireMeasureDingeChanged(h => { });

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
            _qingdanGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "btnViewImage", HeaderText = "图片", Text = "查看",
                UseColumnTextForButtonValue = true, Width = 60
            });
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
