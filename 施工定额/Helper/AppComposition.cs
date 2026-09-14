using 施工定额.Service;

namespace 施工定额.Helper
{
    /// <summary>
    /// 简单组合根：集中创建仓储 / 计算 / 导入等依赖，避免 Form 里散落 new。
    /// 不引入第三方 DI 容器，保持 WinForms 项目轻量。
    /// </summary>
    public static class AppComposition
    {
        public static IQingdanRepository CreateQingdanRepository() =>
            new QingdanRepository(AppConfig.UserDbConn);

        public static ICostCalculationService CreateCostCalculationService() =>
            new CostCalculationService(AppConfig.FeeRates);

        public static ICostCalculationService CreateCostCalculationService(Entity.FeeRateSettings rates) =>
            new CostCalculationService(rates);

        public static UnitProjectController CreateUnitProjectController()
        {
            var conn = AppConfig.UserDbConn;
            return new UnitProjectController(
                new UnitProjectRepository(conn),
                new OtherProjectRepository(conn));
        }

        public static IImportService CreateImportService() =>
            new ImportService(AppConfig.SystemDbConn, AppConfig.UserDbConn);

        public static IAppCache Cache => AppCache.Instance;

        public static Form2 CreateImportForm(string targetQingdanCode, string? qingdanCategory = null,
            string? unitProjectCode = null) =>
            new Form2(targetQingdanCode, CreateImportService(), Cache, qingdanCategory, unitProjectCode);
    }
}
