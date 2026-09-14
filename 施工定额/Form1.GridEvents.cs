using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.UI;

namespace 施工定额
{
    public partial class Form1
    {
        private void OnQingdanSelectionChanged(object? sender, string code)
        {
            UpdateDisplay(DisplayType.Dinge);
            UpdateDisplay(DisplayType.Xiaohaoliang);
        }

        private void OnDingeSelectionChanged(object? sender, (string code, string id) _)
        {
            UpdateDisplay(DisplayType.Xiaohaoliang);
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.SelectQingdan(dataGridView1, e.RowIndex);

        private void dataGridView_measure_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null) return;
            _gridCoordinator.SelectQingdan(dataGridView_measure, e.RowIndex);
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.OpenImportFormFromGrid(dataGridView1, e.RowIndex);

        private void dataGridView_measure_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null) return;
            _gridCoordinator.OpenImportFormFromGrid(dataGridView_measure, e.RowIndex);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e) { }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.ShowImage(dataGridView1, e.RowIndex, e.ColumnIndex);

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex < 0) return;
            string selectedTabName = tabControl1.TabPages[tabControl1.SelectedIndex].Name;
            if (selectedTabName == "tabRenCaiJi")
                dataGridView3.DataSource = _summaryPresenter.GetRenCaiJiSummaryFromMemory("", CurrentUnitProjectCode);
            if (selectedTabName == "tabCostSummary")
                dataGridView4.DataSource = _summaryPresenter.GetCostSummaryData(CurrentUnitProjectCode);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            dataGridView3.DataSource = _summaryPresenter.GetRenCaiJiSummaryFromMemory(e.Node?.Text ?? "", CurrentUnitProjectCode);
        }

        private void dataGridView2_CellValueChanged(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.HandleXiaohaoliangCellValueChanged(
                dataGridView2,
                dataGridView2.DataSource as BindingList<Xiaohaoliang> ?? new BindingList<Xiaohaoliang>(),
                e,
                _suppressGridEvents);

        private void DataGridView_dinge_CellValueChanged(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.HandleDingeCellValueChanged(
                DataGridView_dinge,
                _dingeBindingList,
                e,
                _suppressGridEvents);

        private void DataGridView_measure_dinge_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (DataGridView_measure_dinge == null) return;
            _gridCoordinator.HandleDingeCellValueChanged(
                DataGridView_measure_dinge,
                _measureDingeBindingList,
                e,
                _suppressGridEvents);
        }

        private void DataGridView_dinge_CellClick(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.SelectDinge(DataGridView_dinge, e.RowIndex);

        private void DataGridView_measure_dinge_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (DataGridView_measure_dinge == null) return;
            _gridCoordinator.SelectDinge(DataGridView_measure_dinge, e.RowIndex);
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e) =>
            _gridCoordinator.HandleQingdanCellEndEdit(dataGridView1, myMemoryQingdanBindingList, e);

        private void dataGridView_measure_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null) return;
            _gridCoordinator.HandleQingdanCellEndEdit(dataGridView_measure, _measureQingdanList, e);
        }
    }
}
