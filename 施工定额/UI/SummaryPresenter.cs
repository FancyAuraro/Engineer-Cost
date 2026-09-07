using System.ComponentModel;
using 施工定额.Entity;
using 施工定额.Service;

namespace 施工定额.UI
{
    public class SummaryPresenter
    {
        private readonly BindingList<Qingdan> _qingdanList;
        private ICostCalculationService _calcService;

        public SummaryPresenter(BindingList<Qingdan> qingdanList, ICostCalculationService calcService)
        {
            _qingdanList = qingdanList;
            _calcService = calcService;
        }

        public void ReplaceCalcService(ICostCalculationService calcService)
        {
            _calcService = calcService ?? throw new ArgumentNullException(nameof(calcService));
        }

        /// <summary>
        /// 按单位工程筛选。分部分项 / 措施 / 其他项目均只统计当前单位工程。
        /// </summary>
        private IEnumerable<Qingdan> Scope(string? unitProjectCode)
        {
            if (string.IsNullOrWhiteSpace(unitProjectCode))
                return _qingdanList;
            var u = unitProjectCode.Trim();
            return _qingdanList.Where(q =>
            {
                var code = string.IsNullOrWhiteSpace(q.单位工程编码)
                    ? UnitProject.DefaultCode
                    : q.单位工程编码.Trim();
                return code == u;
            });
        }

        public object GetCostSummaryData(string? unitProjectCode = null)
        {
            var summary = _calcService.CalculateProjectSummary(Scope(unitProjectCode));
            return new List<object>
            {
                new { Name = "分部分项费用", Price = summary.分部分项合价 },
                new { Name = "措施项目费用", Price = summary.措施项目合价 },
                new { Name = "其他项目费用", Price = summary.其他项目合价 },
                new { Name = "人工费",       Price = summary.人工费 },
                new { Name = "材料费",       Price = summary.材料费 },
                new { Name = "机械费",       Price = summary.机械费 },
                new { Name = "管理费",       Price = summary.管理费 },
                new { Name = "利润",         Price = summary.利润 },
                new { Name = "规费",         Price = summary.规费 },
                new { Name = "增值税",       Price = summary.税金 },
                new { Name = "不含税总价",   Price = summary.不含税总价 },
                new { Name = "含税总价",     Price = summary.含税总价 },
            };
        }

        public List<XiaohaoliangSummary> GetRenCaiJiSummaryFromMemory(string category, string? unitProjectCode = null)
        {
            return Scope(unitProjectCode)
                .SelectMany(q => q.定额列表)
                .SelectMany(d => d.消耗量列表)
                .Where(x => string.IsNullOrEmpty(category) || x.消耗量类别 == category)
                .GroupBy(x => new { x.消耗量类别, x.消耗量编码, x.消耗量名称, x.规格型号, x.消耗量单位 })
                .Select(g => new XiaohaoliangSummary
                {
                    消耗量类别 = g.Key.消耗量类别,
                    消耗量编码 = g.Key.消耗量编码,
                    消耗量名称 = g.Key.消耗量名称,
                    规格型号 = g.Key.规格型号,
                    消耗量单位 = g.Key.消耗量单位,
                    市场价 = g.Max(x => x.市场价),
                    市场价合计 = g.Sum(x => x.市场价合计)
                })
                .ToList();
        }
    }
}
