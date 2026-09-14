using System.ComponentModel;
using 施工定额;
using 施工定额.Entity;
using 施工定额.Service;
using 施工定额.UI;
using Xunit;

namespace 施工定额.Tests
{
    public class QingdanPresenterTests
    {
        private sealed class FakeRepo : IQingdanRepository
        {
            public List<Qingdan> Tree { get; set; } = new();
            public int SaveTreeCalls { get; private set; }
            public int SaveQingdanCalls { get; private set; }
            public int SaveHeaderCalls { get; private set; }
            public int SaveDingeCalls { get; private set; }

            public List<Qingdan> LoadTree() => Tree.ToList();
            public void SaveTree(Qingdan qd) { SaveTreeCalls++; }
            public void SaveQingdan(Qingdan qd) { SaveQingdanCalls++; }
            public void SaveQingdanHeader(Qingdan qd) { SaveHeaderCalls++; }
            public void SaveDinge(Dinge dg) { SaveDingeCalls++; }
            public void SaveXiaohaoliang(Xiaohaoliang xhl) { }
            public void UpdateMarketPriceByCode(string 消耗量编码, decimal 新市场价) { }
            public void DeleteQingdan(string qingdanCode) { }
        }

        private static Dinge SampleDinge(string id = "dg1", decimal work = 10m, decimal factor = 1m)
        {
            var dg = new Dinge
            {
                ID号 = id,
                定额编码 = "D-001",
                定额工程量 = work,
                换算系数 = factor,
            };
            dg.消耗量列表.Add(new Xiaohaoliang
            {
                定额ID = id,
                消耗量类别 = "人",
                消耗量编码 = "L1",
                含量 = 2m,
                市场价 = 100m
            });
            return dg;
        }

        [Fact]
        public void OnDingeConversionFactorChanged_UpdatesWorkAmountAndUsesFineSave()
        {
            var repo = new FakeRepo();
            var list = new BindingList<Qingdan>();
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 10m };
            var dg = SampleDinge("dg1", 10m, 1m);
            qd.定额列表.Add(dg);
            list.Add(qd);

            var calc = new CostCalculationService(new FeeRateSettings
            {
                OverheadRate = 0,
                ProfitRate = 0,
                VatRate = 0
            });
            var presenter = new QingdanPresenter(repo, calc, list, _ => { });

            dg.换算系数 = 2m;
            presenter.OnDingeConversionFactorChanged(qd, dg);

            Assert.Equal(20m, dg.定额工程量);
            Assert.Equal(0, repo.SaveTreeCalls);
            Assert.True(repo.SaveQingdanCalls >= 1);
        }

        [Fact]
        public void OnQingdanWorkAmountChanged_AppliesFactorPerDinge()
        {
            var repo = new FakeRepo();
            var list = new BindingList<Qingdan>();
            var qd = new Qingdan { 清单编码 = "Q1", 工程量 = 5m };
            var dg1 = SampleDinge("dg1", 5m, 1m);
            var dg2 = SampleDinge("dg2", 10m, 2m);
            qd.定额列表.Add(dg1);
            qd.定额列表.Add(dg2);
            list.Add(qd);

            var calc = new CostCalculationService(new FeeRateSettings
            {
                OverheadRate = 0,
                ProfitRate = 0,
                VatRate = 0
            });
            var presenter = new QingdanPresenter(repo, calc, list, _ => { });

            qd.工程量 = 8m;
            presenter.OnQingdanWorkAmountChanged(qd);

            Assert.Equal(8m, dg1.定额工程量);
            Assert.Equal(16m, dg2.定额工程量);
            Assert.Equal(0, repo.SaveTreeCalls);
            Assert.True(repo.SaveQingdanCalls >= 1);
        }

        [Fact]
        public void OnMarketPriceChanged_UpdatesAllMatchingCodes()
        {
            var repo = new FakeRepo();
            var list = new BindingList<Qingdan>();

            var qd1 = new Qingdan { 清单编码 = "Q1", 工程量 = 1m };
            var dg1 = SampleDinge("dg1", 1m, 1m);
            qd1.定额列表.Add(dg1);
            list.Add(qd1);

            var qd2 = new Qingdan { 清单编码 = "Q2", 工程量 = 1m };
            var dg2 = SampleDinge("dg2", 1m, 1m);
            qd2.定额列表.Add(dg2);
            list.Add(qd2);

            repo.Tree = list.ToList();

            var calc = new CostCalculationService(new FeeRateSettings
            {
                OverheadRate = 0,
                ProfitRate = 0,
                VatRate = 0
            });

            var presenter = new QingdanPresenter(repo, calc, list, _ => { });
            presenter.ReloadAll();

            var xhl = list[0].定额列表[0].消耗量列表[0];
            presenter.OnMarketPriceChanged(xhl, 150m);

            Assert.Equal(150m, list[0].定额列表[0].消耗量列表[0].市场价);
            Assert.Equal(150m, list[1].定额列表[0].消耗量列表[0].市场价);
            Assert.True(repo.SaveQingdanCalls >= 1);
        }
    }
}
