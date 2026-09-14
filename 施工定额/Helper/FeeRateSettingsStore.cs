using 施工定额.Entity;
using Microsoft.Extensions.Configuration;

namespace 施工定额.Helper
{
    /// <summary>
    /// 费率配置的加载与持久化，隔离 AppConfig 的配置职责。
    /// </summary>
    public sealed class FeeRateSettingsStore
    {
        private readonly IConfiguration _configuration;
        private readonly string _path;

        public FeeRateSettingsStore(IConfiguration configuration, string dataDirectory)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _path = Path.Combine(dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory)), "fee_settings.json");
        }

        public FeeRateSettings Load()
        {
            var settings = new FeeRateSettings();
            var section = _configuration.GetSection("FeeSettings");
            if (section.Exists())
            {
                settings.OverheadBase = section["OverheadBase"] ?? settings.OverheadBase;
                if (decimal.TryParse(section["OverheadRate"], out var overhead)) settings.OverheadRate = overhead;
                if (decimal.TryParse(section["ProfitRate"], out var profit)) settings.ProfitRate = profit;
                if (decimal.TryParse(section["StatutoryFeeRate"], out var statutory)) settings.StatutoryFeeRate = statutory;
                if (decimal.TryParse(section["VatRate"], out var vat)) settings.VatRate = vat;
                if (bool.TryParse(section["IncludeStatutoryInUnitPrice"], out var include)) settings.IncludeStatutoryInUnitPrice = include;
            }

            try
            {
                if (!File.Exists(_path))
                    return settings;

                var json = File.ReadAllText(_path);
                var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
                if (root.TryGetProperty("OverheadBase", out var ob) && ob.GetString() is string baseName && !string.IsNullOrWhiteSpace(baseName))
                    settings.OverheadBase = baseName;
                if (root.TryGetProperty("OverheadRate", out var ohr) && ohr.TryGetDecimal(out var overhead)) settings.OverheadRate = overhead;
                if (root.TryGetProperty("ProfitRate", out var pr) && pr.TryGetDecimal(out var profit)) settings.ProfitRate = profit;
                if (root.TryGetProperty("StatutoryFeeRate", out var sr) && sr.TryGetDecimal(out var statutory)) settings.StatutoryFeeRate = statutory;
                if (root.TryGetProperty("VatRate", out var vr) && vr.TryGetDecimal(out var vat)) settings.VatRate = vat;
                if (root.TryGetProperty("IncludeStatutoryInUnitPrice", out var ir) &&
                    (ir.ValueKind == System.Text.Json.JsonValueKind.True || ir.ValueKind == System.Text.Json.JsonValueKind.False))
                    settings.IncludeStatutoryInUnitPrice = ir.GetBoolean();
            }
            catch (Exception ex)
            {
                AppLogger.Error("读取用户费率配置失败", ex);
            }

            return settings;
        }

        public void Save(FeeRateSettings rates)
        {
            if (rates == null) throw new ArgumentNullException(nameof(rates));
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                rates.OverheadBase,
                rates.OverheadRate,
                rates.ProfitRate,
                rates.StatutoryFeeRate,
                rates.VatRate,
                rates.IncludeStatutoryInUnitPrice
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
    }
}
