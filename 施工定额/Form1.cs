using System.ComponentModel;
using System.Diagnostics;
using 施工定额.Entity;
using 施工定额.Export;
using 施工定额.Helper;
using 施工定额.Service;
using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1 : Form
    {
        private readonly BindingList<Qingdan> _allQingdan = new BindingList<Qingdan>();
        private readonly BindingList<Qingdan> myMemoryQingdanBindingList = new BindingList<Qingdan>();
        private readonly BindingList<Qingdan> _measureQingdanList = new BindingList<Qingdan>();
        private readonly BindingList<Dinge> _dingeBindingList = new BindingList<Dinge>();
        private readonly BindingList<Dinge> _measureDingeBindingList = new BindingList<Dinge>();
        private readonly BindingList<Xiaohaoliang> _xhlBindingList = new BindingList<Xiaohaoliang>();

        private readonly IQingdanRepository _repo;
        private ICostCalculationService _calcService;
        private readonly SelectionState _selection;
        private readonly QingdanPresenter _qingdanPresenter;
        private readonly SummaryPresenter _summaryPresenter;
        private readonly ContextMenuBuilder _menuBuilder;
        private readonly Form1DisplayCoordinator _displayCoordinator;

        private TabPage? tabPage措施;
        private SplitContainer? measureSplit;
        private FeatureDataGridView? dataGridView_measure;
        private DataGridView? DataGridView_measure_dinge;
        private bool _suppressGridEvents;

        public void ReloadAndRecalculateEverything()
        {
            _qingdanPresenter.ReloadAll();
            RebuildCategoryViews();
            var stillExists = _allQingdan.Any(q => q.清单编码 == _selection.SelectedQingdanCode);
            if (!stillExists)
                _selection.SelectQingdan("");
            UpdateDisplay(DisplayType.Qingdan);
            UpdateDisplay(DisplayType.Dinge);
            UpdateDisplay(DisplayType.Xiaohaoliang);
        }

        private void RebuildCategoryViews() => _displayCoordinator.RebuildCategoryViews();

        public Form1()
            : this(AppComposition.CreateQingdanRepository(), AppComposition.CreateCostCalculationService())
        {
        }

        public Form1(IQingdanRepository repo, ICostCalculationService calcService)
        {
            InitializeComponent();
            ApplyUiFont();
            ApplyResponsiveLayout();
            EnsureMeasureTab();
            ApplyToolbarIcons();
            Text = "施工定额";

            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _calcService = calcService ?? throw new ArgumentNullException(nameof(calcService));
            _selection = new SelectionState();

            _qingdanPresenter = new QingdanPresenter(_repo, _calcService, _allQingdan, UpdateDisplay);
            _summaryPresenter = new SummaryPresenter(_allQingdan, _calcService);
            _displayCoordinator = new Form1DisplayCoordinator(
                _allQingdan,
                myMemoryQingdanBindingList,
                _measureQingdanList,
                _dingeBindingList,
                _measureDingeBindingList,
                _xhlBindingList,
                () => CurrentUnitProjectCode,
                code => _allQingdan.FirstOrDefault(q => q.清单编码 == code),
                value => _suppressGridEvents = value);

            _menuBuilder = new ContextMenuBuilder(
                _qingdanPresenter, _selection, ReloadAndRecalculateEverything,
                () => CurrentUnitProjectCode);
            dataGridView1.Tag = QingdanCategory.分部分项;
            dataGridView1.ContextMenuStrip = _menuBuilder.BuildQingdanMenu(dataGridView1, QingdanCategory.分部分项);

            _selection.QingdanSelectionChanged += OnQingdanSelectionChanged;
            _selection.DingeSelectionChanged += OnDingeSelectionChanged;

            EnsureSettingsMenu();
            EnsureHelpMenu();
            WirePlaceholderMenus();
            UiTheme.ApplyToolStrip(menuStrip1);
            EnsureUnitProjectsInitialized();
        }

        private void EnsureMeasureTab()
        {
            if (tabControl1.TabPages.Cast<TabPage>().Any(t => t.Name == "tabPage措施"))
                return;

            tabPage措施 = new TabPage
            {
                Name = "tabPage措施",
                Text = "措施项目",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3)
            };

            measureSplit = new SplitContainer
            {
                Name = "measureSplit",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };

            dataGridView_measure = new FeatureDataGridView
            {
                Dock = DockStyle.Fill,
                Name = "dataGridView_measure",
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            dataGridView_measure.CellClick += dataGridView_measure_CellClick;
            dataGridView_measure.CellDoubleClick += dataGridView_measure_CellDoubleClick;
            dataGridView_measure.CellEndEdit += dataGridView_measure_CellEndEdit;

            DataGridView_measure_dinge = new DataGridView
            {
                Dock = DockStyle.Fill,
                Name = "DataGridView_measure_dinge",
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            DataGridView_measure_dinge.CellClick += DataGridView_measure_dinge_CellClick;
            DataGridView_measure_dinge.CellValueChanged += DataGridView_measure_dinge_CellValueChanged;

            measureSplit.Panel1.Controls.Add(dataGridView_measure);
            measureSplit.Panel2.Controls.Add(DataGridView_measure_dinge);
            tabPage措施.Controls.Add(measureSplit);

            var insertAt = 1;
            for (int i = 0; i < tabControl1.TabPages.Count; i++)
            {
                if (tabControl1.TabPages[i].Name == "tabPage1")
                {
                    insertAt = i + 1;
                    break;
                }
            }
            tabControl1.TabPages.Insert(insertAt, tabPage措施);
            // 分隔位置由 Form1.Layout + SplitterLayoutStore 统一处理
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeGridColumns();
            ReloadAndRecalculateEverything();
        }

        private void InitializeGridColumns()
        {
            GridManager.BindOnce(dataGridView1, myMemoryQingdanBindingList, GridColumns.Qingdan);
            dataGridView1.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "btnViewImage", HeaderText = "图片", Text = "查看",
                UseColumnTextForButtonValue = true, Width = 60
            });
            GridLayoutStore.Attach(dataGridView1);
            GridManager.BindOnce(DataGridView_dinge, _dingeBindingList, GridColumns.Dinge);
            GridManager.BindOnce(dataGridView2, _xhlBindingList, GridColumns.Xiaohaoliang);
            if (dataGridView_measure != null)
            {
                GridManager.BindOnce(dataGridView_measure, _measureQingdanList, GridColumns.MeasureQingdan);
                dataGridView_measure.Tag = QingdanCategory.措施项目;
                dataGridView_measure.ContextMenuStrip = _menuBuilder.BuildQingdanMenu(dataGridView_measure, QingdanCategory.措施项目);
            }
            if (DataGridView_measure_dinge != null)
                GridManager.BindOnce(DataGridView_measure_dinge, _measureDingeBindingList, GridColumns.Dinge);
        }

        public void UpdateDisplay(DisplayType type) =>
            _displayCoordinator.UpdateDisplay(
                type,
                _selection.SelectedQingdanCode,
                _selection.SelectedDingeCode,
                _selection.SelectedDingeID);
    }
}
