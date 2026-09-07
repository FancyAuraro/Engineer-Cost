using System.ComponentModel;
using System.Diagnostics;
using 施工定额.Entity;
using 施工定额.Export;
using 施工定额.Helper;
using 施工定额.Service;
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

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            _selection.SelectQingdan(dataGridView1.Rows[e.RowIndex].Cells["清单编码"].Value?.ToString() ?? "");
        }

        private void dataGridView_measure_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null || e.RowIndex < 0) return;
            _selection.SelectQingdan(dataGridView_measure.Rows[e.RowIndex].Cells["清单编码"].Value?.ToString() ?? "");
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e) =>
            OpenImportFormFromGrid(dataGridView1, e.RowIndex);

        private void dataGridView_measure_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null) return;
            OpenImportFormFromGrid(dataGridView_measure, e.RowIndex);
        }

        private void OpenImportFormFromGrid(DataGridView grid, int rowIndex)
        {
            if (rowIndex < 0) return;
            string qingdanCode = grid.Rows[rowIndex].Cells["清单编码"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(qingdanCode))
            {
                ErrorHandler.ShowBusiness("当前选中的清单编码为空，无法打开定额库。");
                return;
            }
            string category = QingdanCategory.分部分项;
            if (grid == dataGridView_measure || (grid.Tag is string tag && QingdanCategory.IsMeasure(tag)))
                category = QingdanCategory.措施项目;
            Form2 f2 = AppComposition.CreateImportForm(qingdanCode, category, CurrentUnitProjectCode);
            f2.DataImported += () =>
            {
                if (IsDisposed) return;
                if (InvokeRequired) Invoke(new Action(ReloadAndRecalculateEverything));
                else ReloadAndRecalculateEverything();
            };
            f2.Show();
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e) { }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dataGridView1.Columns[e.ColumnIndex].Name != "btnViewImage") return;
            string code = dataGridView1.Rows[e.RowIndex].Cells["清单编码"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(code)) return;
            string imageFolder = Path.Combine(AppConfig.DataDirectory, "images", code);
            if (!Directory.Exists(imageFolder))
                imageFolder = Path.Combine(AppContext.BaseDirectory, code);
            if (!Directory.Exists(imageFolder))
            {
                ErrorHandler.ShowBusiness($"未找到图片文件夹：{imageFolder}");
                return;
            }
            var supportedExt = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var imageFiles = Directory.GetFiles(imageFolder)
                .Where(f => supportedExt.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            if (imageFiles.Count == 0)
            {
                ErrorHandler.ShowBusiness("该清单文件夹下没有图片。");
                return;
            }
            new ImageViewerForm(code, imageFiles).Show(this);
        }

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

        private void dataGridView2_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressGridEvents || e.RowIndex < 0) return;
            dataGridView2.CommitEdit(DataGridViewDataErrorContexts.Commit);
            var source = dataGridView2.DataSource as BindingList<Xiaohaoliang>;
            if (source == null || e.RowIndex >= source.Count) return;
            var xhl = source[e.RowIndex];
            var colName = dataGridView2.Columns[e.ColumnIndex].Name;
            var cellValue = dataGridView2.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            try
            {
                switch (colName)
                {
                    case "市场价":
                        if (decimal.TryParse(cellValue?.ToString(), out var p))
                            _qingdanPresenter.OnMarketPriceChanged(xhl, p);
                        return;
                    case "含量":
                        if (decimal.TryParse(cellValue?.ToString(), out var c))
                            _qingdanPresenter.OnXiaohaoliangHanliangChanged(xhl, c);
                        return;
                    default: return;
                }
            }
            catch (Exception ex) { ErrorHandler.Show(ex); }
        }

        private void HandleDingeCellValueChanged(DataGridView grid, BindingList<Dinge> binding, DataGridViewCellEventArgs e)
        {
            if (_suppressGridEvents || e.RowIndex < 0) return;
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            var currentQd = _allQingdan.FirstOrDefault(q => q.清单编码 == _selection.SelectedQingdanCode);
            if (currentQd == null) return;
            _suppressGridEvents = true;
            try
            {
                var colName = grid.Columns[e.ColumnIndex].Name;
                if (colName == "换算系数")
                {
                    var dg = binding.ElementAtOrDefault(e.RowIndex);
                    if (dg != null) _qingdanPresenter.OnDingeConversionFactorChanged(currentQd, dg);
                    else _qingdanPresenter.OnDingeChanged(currentQd);
                }
                else _qingdanPresenter.OnDingeChanged(currentQd);
            }
            catch (Exception ex) { ErrorHandler.Show(ex); }
            finally
            {
                BeginInvoke(new Action(() => { if (!IsDisposed) _suppressGridEvents = false; }));
            }
        }

        private void DataGridView_dinge_CellValueChanged(object sender, DataGridViewCellEventArgs e) =>
            HandleDingeCellValueChanged(DataGridView_dinge, _dingeBindingList, e);

        private void DataGridView_measure_dinge_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (DataGridView_measure_dinge == null) return;
            HandleDingeCellValueChanged(DataGridView_measure_dinge, _measureDingeBindingList, e);
        }

        private void DataGridView_dinge_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string code = DataGridView_dinge.Rows[e.RowIndex].Cells["定额编码"].Value?.ToString() ?? "";
            string id = DataGridView_dinge.Rows[e.RowIndex].Cells["ID号"].Value?.ToString() ?? "";
            _selection.SelectDinge(code, id);
        }

        private void DataGridView_measure_dinge_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (DataGridView_measure_dinge == null || e.RowIndex < 0) return;
            string code = DataGridView_measure_dinge.Rows[e.RowIndex].Cells["定额编码"].Value?.ToString() ?? "";
            string id = DataGridView_measure_dinge.Rows[e.RowIndex].Cells["ID号"].Value?.ToString() ?? "";
            _selection.SelectDinge(code, id);
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e) =>
            HandleQingdanCellEndEdit(dataGridView1, myMemoryQingdanBindingList, e);

        private void dataGridView_measure_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView_measure == null) return;
            HandleQingdanCellEndEdit(dataGridView_measure, _measureQingdanList, e);
        }

        private void HandleQingdanCellEndEdit(DataGridView grid, BindingList<Qingdan> list, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var colName = grid.Columns[e.ColumnIndex].Name;
            var changedQd = list.ElementAtOrDefault(e.RowIndex);
            if (changedQd == null) return;
            try
            {
                if (colName == "工程量") { _qingdanPresenter.OnQingdanWorkAmountChanged(changedQd); return; }
                _qingdanPresenter.SaveQingdanFields(changedQd);
            }
            catch (Exception ex) { ErrorHandler.Show(ex); }
        }
    }
}
