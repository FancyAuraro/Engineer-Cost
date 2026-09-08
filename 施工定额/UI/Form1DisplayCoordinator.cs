using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.Helper;

namespace 施工定额.UI
{
    /// <summary>
    /// 主窗体的显示状态协调：分类列表、定额列表、消耗量列表。
    /// 不负责业务计算或持久化，只把领域数据映射到 WinForms 绑定列表。
    /// </summary>
    public sealed class Form1DisplayCoordinator
    {
        private readonly BindingList<Qingdan> _allQingdan;
        private readonly BindingList<Qingdan> _fenbuQingdan;
        private readonly BindingList<Qingdan> _measureQingdan;
        private readonly BindingList<Dinge> _dinge;
        private readonly BindingList<Dinge> _measureDinge;
        private readonly BindingList<Xiaohaoliang> _xiaohaoliang;
        private readonly Func<string> _currentUnitCode;
        private readonly Func<string, Qingdan?> _findQingdan;
        private readonly Action<bool> _setSuppressGridEvents;

        public Form1DisplayCoordinator(
            BindingList<Qingdan> allQingdan,
            BindingList<Qingdan> fenbuQingdan,
            BindingList<Qingdan> measureQingdan,
            BindingList<Dinge> dinge,
            BindingList<Dinge> measureDinge,
            BindingList<Xiaohaoliang> xiaohaoliang,
            Func<string> currentUnitCode,
            Func<string, Qingdan?> findQingdan,
            Action<bool> setSuppressGridEvents)
        {
            _allQingdan = allQingdan;
            _fenbuQingdan = fenbuQingdan;
            _measureQingdan = measureQingdan;
            _dinge = dinge;
            _measureDinge = measureDinge;
            _xiaohaoliang = xiaohaoliang;
            _currentUnitCode = currentUnitCode;
            _findQingdan = findQingdan;
            _setSuppressGridEvents = setSuppressGridEvents;
        }

        public void RebuildCategoryViews()
        {
            _fenbuQingdan.RaiseListChangedEvents = false;
            _measureQingdan.RaiseListChangedEvents = false;
            try
            {
                _fenbuQingdan.Clear();
                _measureQingdan.Clear();
                var unitCode = _currentUnitCode();
                foreach (var qd in _allQingdan)
                {
                    if (QingdanCategory.IsOther(qd.项目类别)) continue;
                    var qdUnit = string.IsNullOrWhiteSpace(qd.单位工程编码)
                        ? UnitProject.DefaultCode
                        : qd.单位工程编码.Trim();
                    if (!string.Equals(qdUnit, unitCode, StringComparison.Ordinal)) continue;
                    if (QingdanCategory.IsMeasure(qd.项目类别))
                        _measureQingdan.Add(qd);
                    else
                        _fenbuQingdan.Add(qd);
                }
            }
            finally
            {
                _fenbuQingdan.RaiseListChangedEvents = true;
                _measureQingdan.RaiseListChangedEvents = true;
                _fenbuQingdan.ResetBindings();
                _measureQingdan.ResetBindings();
            }
        }

        public void UpdateDisplay(DisplayType type, string selectedQingdanCode, string selectedDingeCode, string selectedDingeId)
        {
            _setSuppressGridEvents(true);
            try
            {
                switch (type)
                {
                    case DisplayType.Qingdan:
                        break;
                    case DisplayType.Dinge:
                    {
                        var currentQd = _findQingdan(selectedQingdanCode);
                        var list = currentQd?.定额列表 ?? Enumerable.Empty<Dinge>();
                        if (currentQd != null && QingdanCategory.IsMeasure(currentQd.项目类别))
                        {
                            _measureDinge.ReplaceAll(list);
                            _dinge.ReplaceAll(Enumerable.Empty<Dinge>());
                        }
                        else
                        {
                            _dinge.ReplaceAll(list);
                            _measureDinge.ReplaceAll(Enumerable.Empty<Dinge>());
                        }
                        break;
                    }
                    case DisplayType.Xiaohaoliang:
                    {
                        var currentQd = _findQingdan(selectedQingdanCode);
                        var currentDg = currentQd?.定额列表.FirstOrDefault(
                            d => d.定额编码 == selectedDingeCode && d.ID号 == selectedDingeId);
                        _xiaohaoliang.ReplaceAll(currentDg?.消耗量列表 ?? Enumerable.Empty<Xiaohaoliang>());
                        break;
                    }
                }
            }
            finally
            {
                _setSuppressGridEvents(false);
            }
        }
    }
}
