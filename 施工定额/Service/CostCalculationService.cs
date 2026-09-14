using 施工定额.Entity;
using 施工定额.Helper;

namespace 施工定额.Service
{
    public class CostCalculationService : ICostCalculationService
    {
        private readonly FeeRateSettings _rates;

        public CostCalculationService() : this(AppConfig.FeeRates) { }

        public CostCalculationService(FeeRateSettings rates)
        {
            _rates = rates ?? new FeeRateSettings();
        }

        public FeeRateSettings Rates => _rates;

        public void RecalculateAll(List<Qingdan> qingdanList)
        {
            if (qingdanList == null) throw new ArgumentNullException(nameof(qingdanList));
            foreach (var qd in qingdanList)
            {
                if (QingdanCategory.IsOther(qd.项目类别))
                    continue;
                RecalculateQingdan(qd);
            }
        }

        public void RecalculateQingdan(Qingdan qd)
        {
            if (qd == null) throw new ArgumentNullException(nameof(qd));
            if (QingdanCategory.IsOther(qd.项目类别)) return;

            foreach (var dg in qd.定额列表)
                RecalculateDinge(dg);

            qd.费用构成.Reset();
            foreach (var dg in qd.定额列表)
                AddBreakdown(qd.费用构成, dg.费用构成);

            qd.费用构成.税金 = CalculateTax(qd.费用构成.不含税合价);
            qd.综合合价 = qd.费用构成.不含税合价;
            qd.综合单价 = qd.工程量 != 0
                ? Math.Round(qd.综合合价 / qd.工程量, 2)
                : 0;
        }

        public void RecalculateDinge(Dinge dg)
        {
            if (dg == null) throw new ArgumentNullException(nameof(dg));
            foreach (var xhl in dg.消耗量列表)
                RecalculateXiaohaoliang(xhl, dg.定额工程量);

            ApplyFeeBreakdown(dg.费用构成, dg.消耗量列表);
            dg.定额合价 = dg.费用构成.不含税合价;
            dg.定额单价 = dg.定额工程量 != 0
                ? Math.Round(dg.定额合价 / dg.定额工程量, 2)
                : 0;
        }

        /// <summary>
        /// 根据人材机消耗量计算完整费用构成。所有定额及项目汇总均复用同一套取费规则。
        /// </summary>
        public void ApplyFeeBreakdown(CostBreakdown breakdown, IEnumerable<Xiaohaoliang> xhlList)
        {
            if (breakdown == null) throw new ArgumentNullException(nameof(breakdown));
            if (xhlList == null) throw new ArgumentNullException(nameof(xhlList));

            breakdown.Reset();
            foreach (var x in xhlList)
            {
                switch (x.消耗量类别)
                {
                    case "人": breakdown.人工费 += x.市场价合计; break;
                    case "材": breakdown.材料费 += x.市场价合计; break;
                    case "机": breakdown.机械费 += x.市场价合计; break;
                    default: breakdown.材料费 += x.市场价合计; break;
                }
            }

            breakdown.人工费 = Round(breakdown.人工费);
            breakdown.材料费 = Round(breakdown.材料费);
            breakdown.机械费 = Round(breakdown.机械费);

            decimal overheadBase = string.Equals(_rates.OverheadBase, "Labor", StringComparison.OrdinalIgnoreCase)
                ? breakdown.人工费
                : breakdown.直接费;
            breakdown.管理费 = Round(overheadBase * _rates.OverheadRate);
            breakdown.利润 = Round((breakdown.直接费 + breakdown.管理费) * _rates.ProfitRate);
            breakdown.规费 = Round(breakdown.人工费 * _rates.StatutoryFeeRate);
            breakdown.不含税合价 = CalculatePretax(breakdown);
            breakdown.税金 = CalculateTax(breakdown.不含税合价);
        }

        public ProjectCostSummary CalculateProjectSummary(IEnumerable<Qingdan> qingdanList)
        {
            if (qingdanList == null) throw new ArgumentNullException(nameof(qingdanList));
            var list = qingdanList.ToList();
            decimal fenbu = list.Where(q => QingdanCategory.IsFenBu(q.项目类别)).Sum(q => q.综合合价);
            decimal measure = list.Where(q => QingdanCategory.IsMeasure(q.项目类别)).Sum(q => q.综合合价);
            decimal other = list.Where(q => QingdanCategory.IsOther(q.项目类别)).Sum(q => q.综合合价);
            decimal billTotal = fenbu + measure + other;

            decimal labor = list.Sum(q => q.费用构成.人工费);
            decimal material = list.Sum(q => q.费用构成.材料费);
            decimal machine = list.Sum(q => q.费用构成.机械费);
            decimal overhead = list.Sum(q => q.费用构成.管理费);
            decimal profit = list.Sum(q => q.费用构成.利润);
            decimal statutory = _rates.IncludeStatutoryInUnitPrice
                ? list.Sum(q => q.费用构成.规费)
                : Round(labor * _rates.StatutoryFeeRate);
            decimal pretax = CalculateProjectPretax(billTotal, statutory);
            decimal tax = CalculateTax(pretax);

            return new ProjectCostSummary
            {
                分部分项合价 = fenbu,
                措施项目合价 = measure,
                其他项目合价 = other,
                人工费 = labor,
                材料费 = material,
                机械费 = machine,
                管理费 = overhead,
                利润 = profit,
                规费 = statutory,
                税金 = tax,
                不含税总价 = pretax,
                含税总价 = pretax + tax
            };
        }

        private decimal CalculatePretax(CostBreakdown breakdown)
        {
            var total = breakdown.直接费 + breakdown.管理费 + breakdown.利润;
            if (_rates.IncludeStatutoryInUnitPrice)
                total += breakdown.规费;
            return Round(total);
        }

        private decimal CalculateProjectPretax(decimal billTotal, decimal statutory)
        {
            return Round(billTotal + (_rates.IncludeStatutoryInUnitPrice ? 0 : statutory));
        }

        private decimal CalculateTax(decimal pretax) => Round(pretax * _rates.VatRate);

        private static decimal Round(decimal value) => Math.Round(value, 2);

        private static void AddBreakdown(CostBreakdown target, CostBreakdown source)
        {
            target.人工费 += source.人工费;
            target.材料费 += source.材料费;
            target.机械费 += source.机械费;
            target.管理费 += source.管理费;
            target.利润 += source.利润;
            target.规费 += source.规费;
            target.不含税合价 += source.不含税合价;
        }

        private static void RecalculateXiaohaoliang(Xiaohaoliang xhl, decimal dingeWorkAmount)
        {
            xhl.数量 = xhl.含量 * dingeWorkAmount;
            xhl.市场价合计 = Round(xhl.市场价 * xhl.数量);
        }
    }
}
