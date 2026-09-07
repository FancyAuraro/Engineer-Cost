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

        private void RebuildCategoryViews()
        {
            myMemoryQingdanBindingList.RaiseListChangedEvents = false;
            _measureQingdanList.RaiseListChangedEvents = false;
            try
            {
                myMemoryQingdanBindingList.Clear();
                _measureQingdanList.Clear();
                foreach (var qd in _allQingdan)
                {
                    if (QingdanCategory.IsOther(qd.项目类别))
                        continue;
                    if (!MatchesCurrentUnit(qd))
                        continue;
                    if (QingdanCategory.IsMeasure(qd.项目类别))
                        _measureQingdanList.Add(qd);
                    else
                        myMemoryQingdanBindingList.Add(qd);
                }
            }
            finally
            {
                myMemoryQingdanBindingList.RaiseListChangedEvents = true;
                _measureQingdanList.RaiseListChangedEvents = true;
                myMemoryQingdanBindingList.ResetBindings();
                _measureQingdanList.ResetBindings();
            }
        }

        public Form1()
            : this(AppComposition.CreateQingdanRepository(), AppComposition.CreateCostCalculationService())
        {
        }

        public Form1(IQingdanRepository repo, ICostCalculationService calcService)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _calcService = calcService ?? throw new ArgumentNullException(nameof(calcService));
            InitializeComponent();
            EnsureMeasureTab();
            ApplyToolbarIcons();
            ApplyUiFont();
            ApplyResponsiveLayout();
            Shown += OnFirstShownApplySplitters;

            _selection = new SelectionState();
            _selection.QingdanChanged += OnQingdanSelectionChanged;
            _selection.DingeChanged += OnDingeSelectionChanged;

            _qingdanPresenter = new QingdanPresenter(_repo, _calcService, _allQingdan, UpdateDisplay);
            _summaryPresenter = new SummaryPresenter(_allQingdan, _calcService);

            _menuBuilder = new ContextMenuBuilder(
                _qingdanPresenter, _selection, ReloadAndRecalculateEverything,
                () => CurrentUnitProjectCode);
            dataGridView1.Tag = QingdanCategory.分部分项;
            dataGridView1.ContextMenuStrip = _menuBuilder.BuildQingdanMenu(dataGridView1, QingdanCategory.分部分项);

            EnsureMeasureTab();
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
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Name = "measureSplit"
            };

            dataGridView_measure = new FeatureDataGridView
            {
                Dock = DockStyle.Fill,
                Name = "dataGridView_measure",
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Tag = QingdanCategory.措施项目
            };
            DataGridView_measure_dinge = new DataGridView
            {
                Dock = DockStyle.Fill,
                Name = "DataGridView_measure_dinge",
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };

            measureSplit.Panel1.Controls.Add(dataGridView_measure);
            measureSplit.Panel2.Controls.Add(DataGridView_measure_dinge);
            tabPage措施.Controls.Add(measureSplit);

            int insertAt = 0;
            for (int i = 0; i < tabControl1.TabPages.Count; i++)
            {
                if (tabControl1.TabPages[i].Name == "tabPage分部分项" || tabControl1.TabPages[i].Text.Contains("分部分项"))
                {
                    insertAt = i + 1;
                    break;
                }
            }
            tabControl1.TabPages.Insert(insertAt, tabPage措施);

            dataGridView_measure.ContextMenuStrip = _menuBuilder.BuildQingdanMenu(dataGridView_measure, QingdanCategory.措施项目);
            dataGridView_measure.CellClick += dataGridView_measure_CellClick;
            dataGridView_measure.CellDoubleClick += dataGridView_measure_CellDoubleClick;
            dataGridView_measure.CellEndEdit += dataGridView_measure_CellEndEdit;
            DataGridView_measure_dinge.CellClick += DataGridView_measure_dinge_CellClick;
            DataGridView_measure_dinge.CellValueChanged += DataGridView_measure_dinge_CellValueChanged;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeGridColumns();
            ReloadAndRecalculateEverything();
        }

        private void InitializeGridColumns()
        {
            GridManager.BindOnce(dataGridView1, myMemoryQingdanBindingList, GridColumns.Qingdan);
            dataGridView1.CellClick += dataGridView1_CellClick;
            dataGridView1.CellDoubleClick += dataGridView1_CellDoubleClick;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;

            GridManager.BindOnce(DataGridView_dinge, _dingeBindingList, GridColumns.Dinge);
            GridManager.BindOnce(dataGridView2, _xhlBindingList, GridColumns.Xiaohaoliang);

            if (dataGridView_measure != null)
                GridManager.BindOnce(dataGridView_measure, _measureQingdanList, GridColumns.MeasureQingdan);
            if (DataGridView_measure_dinge != null)
                GridManager.BindOnce(DataGridView_measure_dinge, _measureDingeBindingList, GridColumns.Dinge);

            DataGridView_dinge.CellClick += DataGridView_dinge_CellClick;
            DataGridView_dinge.CellValueChanged += DataGridView_dinge_CellValueChanged;
            dataGridView2.CellValueChanged += dataGridView2_CellValueChanged;
        }

        public void UpdateDisplay(DisplayType type)
        {
            switch (type)
            {
                case DisplayType.Qingdan:
                    RebuildCategoryViews();
                    break;
                case DisplayType.Dinge:
                    {
                        _dingeBindingList.RaiseListChangedEvents = false;
                        _measureDingeBindingList.RaiseListChangedEvents = false;
                        try
                        {
                            _dingeBindingList.Clear();
                            _measureDingeBindingList.Clear();
                            var qd = _allQingdan.FirstOrDefault(q => q.清单编码 == _selection.SelectedQingdanCode);
                            if (qd != null)
                            {
                                var target = QingdanCategory.IsMeasure(qd.项目类别)
                                    ? _measureDingeBindingList
                                    : _dingeBindingList;
                                foreach (var d in qd.定额列表)
                                    target.Add(d);
                            }
                        }
                        finally
                        {
                            _dingeBindingList.RaiseListChangedEvents = true;
                            _measureDingeBindingList.RaiseListChangedEvents = true;
                            _dingeBindingList.ResetBindings();
                            _measureDingeBindingList.ResetBindings();
                        }
                        break;
                    }
                case DisplayType.Xiaohaoliang:
                    {
                        _xhlBindingList.RaiseListChangedEvents = false;
                        try
                        {
                            _xhlBindingList.Clear();
                            var qd = _allQingdan.FirstOrDefault(q => q.清单编码 == _selection.SelectedQingdanCode);
                            var dg = qd?.定额列表.FirstOrDefault(d => d.ID号 == _selection.SelectedDingeId);
                            if (dg != null)
                            {
                                foreach (var x in dg.消耗量列表)
                                    _xhlBindingList.Add(x);
                            }
                        }
                        finally
                        {
                            _xhlBindingList.RaiseListChangedEvents = true;
                            _xhlBindingList.ResetBindings();
                        }
                        break;
                    }
            }
        }
    }
}
