using 施工定额.Entity;
using 施工定额.Service;
using Xunit;

namespace 施工定额.Tests
{
    public class CostCalculationServiceTests
    {
        private static CostCalculationService CreateService(
            string overheadBase = "DirectCost",
            decimal overheadRate = 0.08m,
            decimal profitRate = 0.05m,
            decimal statutory = 0m,
            decimal vat = 0.09m,
            bool includeStatutory = false)
        {
            return new CostCalculationService(new FeeRateSettings
            {
                OverheadBase = overheadBase,
                OverheadRate = overheadRate,
                ProfitRate = profitRate,
                StatutoryFeeRate = statutory,
                VatRate = vat,
                IncludeStatutoryInUnitPrice = includeStatutory
            });
        }

        private static Dinge SampleDinge(decimal workAmount = 10m)
        {
            return new Dinge
            {
                ID号 = "dg1", 定额编码 = "D-001", 定额工程量 = workAmount, 换算系数 = 1m,
                消耗量列表 =
                {
                    new Xiaohaoliang { 消耗量类别 = "人", 消耗量编码 = "L1", 含量 = 2m, 市场价 = 100m },
                    new Xiaohaoliang { 消耗量类别 = "材", 消耗量编码 = "M1", 含量 = 5m, 市场价 = 20m },
                    new Xiaohaoliang { 消耗量类别 = "机", 消耗量编码 = "E1", 含量 = 1m, 市场价 = 50m }
                }
            };
        }

        [Fact]
        public void RecalculateDinge_ComputesQuantityAndAmounts()
        {
            var svc = CreateService();
            var dg = SampleDinge(10m);
            svc.RecalculateDinge(dg);
            Assert.Equal(20m, dg.消耗量列表[0].数量);
            Assert.Equal(2000m, dg.消耗量列表[0].市场价合计);
            Assert.Equal(2000m, dg.费用构成.人工费);
            Assert.Equal(1000m, dg.费用构成.材料费);
            Assert.Equal(500m, dg.费用构成.机械费);
            Assert.Equal(3500m, dg.费用构成.直接费);
        }

        [Fact]
        public void RecalculateDinge_OverheadOnDirectCost()
        {
            var svc = CreateService(overheadBase: "DirectCost", overheadRate: 0.08m, profitRate: 0.05m);
            var dg = SampleDinge(10m);
            svc.RecalculateDinge(dg);
            Assert.Equal(280m, dg.费用构成.管理费);
            Assert.Equal(189m, dg.费用构成.利润);
            Assert.Equal(3969m, dg.费用构成.不含税合价);
            Assert.Equal(396.9m, dg.定额单价);
        }

        [Fact]
        public void RecalculateDinge_OverheadOnLabor()
        {
            var svc = CreateService(overheadBase: "Labor", overheadRate: 0.1m, profitRate: 0m);
            var dg = SampleDinge(10m);
            svc.RecalculateDinge(dg);
            Assert.Equal(200m, dg.费用构成.管理费);
            Assert.Equal(3700m, dg.费用构成.不含税合价);
        }

        [Fact]
        public void RecalculateDinge_IncludeStatutoryInUnitPrice()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m, statutory: 0.1m, includeStatutory: true);
            var dg = SampleDinge(10m);
            svc.RecalculateDinge(dg);
            Assert.Equal(200m, dg.费用构成.规费);
            Assert.Equal(3700m, dg.费用构成.不含税合价);
        }

        [Fact]
        public void RecalculateDinge_ZeroWorkAmount_UnitPriceIsZero()
        {
            var svc = CreateService();
            var dg = SampleDinge(0m);
            svc.RecalculateDinge(dg);
            Assert.Equal(0m, dg.定额单价);
            Assert.Equal(0m, dg.消耗量列表[0].数量);
        }

        [Fact]
        public void RecalculateDinge_NegativeWorkAmount_PreservesMathematicalResult()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m);
            var dg = SampleDinge(-2m);
            svc.RecalculateDinge(dg);
            Assert.Equal(-4m, dg.消耗量列表[0].数量);
            Assert.Equal(-400m, dg.消耗量列表[0].市场价合计);
            Assert.Equal(-700m, dg.费用构成.不含税合价);
        }

        [Fact]
        public void RecalculateQingdan_OtherProjectIsNotRecalculated()
        {
            var svc = CreateService();
            var qd = new Qingdan
            {
                清单编码 = "QT-ZJJE", 项目类别 = QingdanCategory.其他项目, 工程量 = 10m,
                综合合价 = 1234.56m, 定额列表 = { SampleDinge(10m) }
            };
            svc.RecalculateQingdan(qd);
            Assert.Equal(1234.56m, qd.综合合价);
            Assert.Equal(0m, qd.定额列表[0].定额合价);
        }

        [Fact]
        public void RecalculateQingdan_AggregatesDingeAndUnitPrice()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m);
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 10m, 定额列表 = { SampleDinge(10m) } };
            svc.RecalculateQingdan(qd);
            Assert.Equal(3500m, qd.综合合价);
            Assert.Equal(350m, qd.综合单价);
            Assert.Equal(315m, qd.费用构成.税金);
        }

        [Fact]
        public void CalculateProjectSummary_AppliesVatOnPretax()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m, vat: 0.09m);
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 10m, 定额列表 = { SampleDinge(10m) } };
            svc.RecalculateQingdan(qd);
            var summary = svc.CalculateProjectSummary(new[] { qd });
            Assert.Equal(3500m, summary.分部分项合价);
            Assert.Equal(315m, summary.税金);
            Assert.Equal(3815m, summary.含税总价);
        }

        [Fact]
        public void CalculateProjectSummary_SeparatesMeasureAndOtherProjects()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m, vat: 0m);
            var fenbu = new Qingdan { 清单编码 = "Q1", 项目类别 = QingdanCategory.分部分项, 综合合价 = 100m };
            var measure = new Qingdan { 清单编码 = "Q2", 项目类别 = QingdanCategory.措施项目, 综合合价 = 20m };
            var other = new Qingdan { 清单编码 = "Q3", 项目类别 = QingdanCategory.其他项目, 综合合价 = 30m };
            var summary = svc.CalculateProjectSummary(new[] { fenbu, measure, other });
            Assert.Equal(100m, summary.分部分项合价);
            Assert.Equal(20m, summary.措施项目合价);
            Assert.Equal(30m, summary.其他项目合价);
            Assert.Equal(150m, summary.含税总价);
        }

        [Fact]
        public void CalculateProjectSummary_ExcludesStatutoryFromBillWhenConfigured()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m, statutory: 0.1m, vat: 0m, includeStatutory: false);
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 10m, 定额列表 = { SampleDinge(10m) } };
            svc.RecalculateQingdan(qd);
            var summary = svc.CalculateProjectSummary(new[] { qd });
            Assert.Equal(3500m, summary.分部分项合价);
            Assert.Equal(200m, summary.规费);
            Assert.Equal(3700m, summary.不含税总价);
            Assert.Equal(3700m, summary.含税总价);
        }

        [Fact]
        public void CalculateProjectSummary_IncludesStatutoryInBillWhenConfigured()
        {
            var svc = CreateService(overheadRate: 0m, profitRate: 0m, statutory: 0.1m, vat: 0m, includeStatutory: true);
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 10m, 定额列表 = { SampleDinge(10m) } };
            svc.RecalculateQingdan(qd);
            var summary = svc.CalculateProjectSummary(new[] { qd });
            Assert.Equal(3700m, summary.分部分项合价);
            Assert.Equal(200m, summary.规费);
            Assert.Equal(3700m, summary.不含税总价);
        }
    }
}
