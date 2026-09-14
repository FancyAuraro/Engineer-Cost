using System.ComponentModel;
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
        private readonly Form1UiSetupCoordinator _uiSetup;
        private readonly Form1GridCoordinator _gridCoordinator;

        private FeatureDataGridView? dataGridView_measure;
        private DataGridView? DataGridView_measure_dinge;
        private bool _suppressGridEvents;

        public Form1()
            : this(AppComposition.CreateQingdanRepository(), AppComposition.CreateCostCalculationService())
        {
        }

        public Form1(IQingdanRepository repo, ICostCalculationService calcService)
        {
            InitializeComponent();
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
            _gridCoordinator = new Form1GridCoordinator(
                this,
                _qingdanPresenter,
                _selection,
                _allQingdan,
                ReloadAndRecalculateEverything,
                () => CurrentUnitProjectCode,
                value => _suppressGridEvents = value);
            _menuBuilder = new ContextMenuBuilder(
                _qingdanPresenter, _selection, ReloadAndRecalculateEverything,
                () => CurrentUnitProjectCode);
            _uiSetup = new Form1UiSetupCoordinator(
                tabControl1,
                dataGridView1,
                DataGridView_dinge,
                dataGridView2,
                myMemoryQingdanBindingList,
                _measureQingdanList,
                _dingeBindingList,
                _measureDingeBindingList,
                _xhlBindingList,
                _menuBuilder,
                dataGridView_measure_CellClick,
                dataGridView_measure_CellDoubleClick,
                dataGridView_measure_CellEndEdit,
                DataGridView_measure_dinge_CellClick,
                DataGridView_measure_dinge_CellValueChanged);

            ApplyUiFont();
            ApplyResponsiveLayout();
            ApplyToolbarIcons();
            Text = "施工定额";
            _uiSetup.EnsureMeasureTab();
            dataGridView_measure = _uiSetup.MeasureQingdanGrid;
            DataGridView_measure_dinge = _uiSetup.MeasureDingeGrid;

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

        public void ReloadAndRecalculateEverything()
        {
            _qingdanPresenter.ReloadAll();
            RebuildCategoryViews();
            if (!_allQingdan.Any(q => q.清单编码 == _selection.SelectedQingdanCode))
                _selection.SelectQingdan("");
            UpdateDisplay(DisplayType.Qingdan);
            UpdateDisplay(DisplayType.Dinge);
            UpdateDisplay(DisplayType.Xiaohaoliang);
        }

        private void RebuildCategoryViews() => _displayCoordinator.RebuildCategoryViews();

        private void Form1_Load(object sender, EventArgs e)
        {
            _uiSetup.InitializeGridColumns();
            ReloadAndRecalculateEverything();
        }

        public void UpdateDisplay(DisplayType type) =>
            _displayCoordinator.UpdateDisplay(
                type,
                _selection.SelectedQingdanCode,
                _selection.SelectedDingeCode,
                _selection.SelectedDingeID);
    }
}
