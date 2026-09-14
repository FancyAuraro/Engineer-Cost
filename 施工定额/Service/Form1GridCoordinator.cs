using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.Export;
using 施工定额.Helper;

namespace 施工定额.Service
{
    /// <summary>
    /// 负责主窗体网格事件中的业务编排。
    /// Form1/GridEvents 只保留 WinForms 事件入口，具体的数据读取、业务判断、Presenter 调用和错误处理集中在这里。
    /// </summary>
    public sealed class Form1GridCoordinator
    {
        private readonly Form _owner;
        private readonly QingdanPresenter _qingdanPresenter;
        private readonly SelectionState _selection;
        private readonly BindingList<Qingdan> _allQingdan;
        private readonly Action _reloadAndRecalculate;
        private readonly Func<string> _currentUnitProjectCode;
        private readonly Action<bool> _setSuppressGridEvents;

        public Form1GridCoordinator(
            Form owner,
            QingdanPresenter qingdanPresenter,
            SelectionState selection,
            BindingList<Qingdan> allQingdan,
            Action reloadAndRecalculate,
            Func<string> currentUnitProjectCode,
            Action<bool> setSuppressGridEvents)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _qingdanPresenter = qingdanPresenter ?? throw new ArgumentNullException(nameof(qingdanPresenter));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _allQingdan = allQingdan ?? throw new ArgumentNullException(nameof(allQingdan));
            _reloadAndRecalculate = reloadAndRecalculate ?? throw new ArgumentNullException(nameof(reloadAndRecalculate));
            _currentUnitProjectCode = currentUnitProjectCode ?? throw new ArgumentNullException(nameof(currentUnitProjectCode));
            _setSuppressGridEvents = setSuppressGridEvents ?? throw new ArgumentNullException(nameof(setSuppressGridEvents));
        }

        public void SelectQingdan(DataGridView grid, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            _selection.SelectQingdan(grid.Rows[rowIndex].Cells["清单编码"].Value?.ToString() ?? "");
        }

        public void OpenImportFormFromGrid(DataGridView grid, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;

            string qingdanCode = grid.Rows[rowIndex].Cells["清单编码"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(qingdanCode))
            {
                ErrorHandler.ShowBusiness("当前选中的清单编码为空，无法打开定额库。");
                return;
            }

            string category = grid.Name == "dataGridView_measure" ||
                (grid.Tag is string tag && QingdanCategory.IsMeasure(tag))
                ? QingdanCategory.措施项目
                : QingdanCategory.分部分项;

            Form2 f2 = AppComposition.CreateImportForm(qingdanCode, category, _currentUnitProjectCode());
            f2.DataImported += () =>
            {
                if (_owner.IsDisposed) return;
                if (_owner.InvokeRequired)
                    _owner.Invoke(new Action(_reloadAndRecalculate));
                else
                    _reloadAndRecalculate();
            };
            f2.Show();
        }

        public void ShowImage(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0 || columnIndex >= grid.Columns.Count) return;
            if (grid.Columns[columnIndex].Name != "btnViewImage") return;

            string code = grid.Rows[rowIndex].Cells["清单编码"].Value?.ToString() ?? "";
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
                .Where(f => supportedExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
            if (imageFiles.Count == 0)
            {
                ErrorHandler.ShowBusiness("该清单文件夹下没有图片。");
                return;
            }

            new ImageViewerForm(code, imageFiles).Show(_owner);
        }

        public void HandleXiaohaoliangCellValueChanged(
            DataGridView grid,
            BindingList<Xiaohaoliang> source,
            DataGridViewCellEventArgs e,
            bool suppressGridEvents)
        {
            if (suppressGridEvents || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            if (e.RowIndex >= source.Count) return;

            var xhl = source[e.RowIndex];
            var colName = grid.Columns[e.ColumnIndex].Name;
            var cellValue = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            try
            {
                switch (colName)
                {
                    case "市场价":
                        if (decimal.TryParse(cellValue?.ToString(), out var price))
                            _qingdanPresenter.OnMarketPriceChanged(xhl, price);
                        break;
                    case "含量":
                        if (decimal.TryParse(cellValue?.ToString(), out var content))
                            _qingdanPresenter.OnXiaohaoliangHanliangChanged(xhl, content);
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex);
            }
        }

        public void HandleDingeCellValueChanged(
            DataGridView grid,
            BindingList<Dinge> binding,
            DataGridViewCellEventArgs e,
            bool suppressGridEvents)
        {
            if (suppressGridEvents || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            var currentQd = _allQingdan.FirstOrDefault(q => q.清单编码 == _selection.SelectedQingdanCode);
            if (currentQd == null) return;

            _setSuppressGridEvents(true);
            try
            {
                var colName = grid.Columns[e.ColumnIndex].Name;
                var dg = binding.ElementAtOrDefault(e.RowIndex);
                if (colName == "换算系数" && dg != null)
                    _qingdanPresenter.OnDingeConversionFactorChanged(currentQd, dg);
                else
                    _qingdanPresenter.OnDingeChanged(currentQd);
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex);
            }
            finally
            {
                _owner.BeginInvoke(new Action(() =>
                {
                    if (!_owner.IsDisposed)
                        _setSuppressGridEvents(false);
                }));
            }
        }

        public void SelectDinge(DataGridView grid, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            string code = grid.Rows[rowIndex].Cells["定额编码"].Value?.ToString() ?? "";
            string id = grid.Rows[rowIndex].Cells["ID号"].Value?.ToString() ?? "";
            _selection.SelectDinge(code, id);
        }

        public void HandleQingdanCellEndEdit(
            DataGridView grid,
            BindingList<Qingdan> list,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= list.Count || e.ColumnIndex < 0) return;

            var changedQd = list[e.RowIndex];
            try
            {
                if (grid.Columns[e.ColumnIndex].Name == "工程量")
                    _qingdanPresenter.OnQingdanWorkAmountChanged(changedQd);
                else
                    _qingdanPresenter.SaveQingdanFields(changedQd);
            }
            catch (Exception ex)
            {
                ErrorHandler.Show(ex);
            }
        }
    }
}
