using System.Globalization;

namespace 施工定额.UI
{
    /// <summary>
    /// 界面数字显示：整数不带小数点，有小数则保留必要位数（最多 4 位）。
    /// </summary>
    public static class UiNumber
    {
        /// <summary>金额类（最多 2 位小数）。</summary>
        public static string Money(decimal value) =>
            value == decimal.Truncate(value)
                ? decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>工程量/含量类（最多 4 位小数）。</summary>
        public static string Quantity(decimal value) =>
            value == decimal.Truncate(value)
                ? decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>给 DataGridView 挂上智能数字格式（对 decimal/double 列生效）。</summary>
        public static void AttachSmartFormat(DataGridView dgv, int maxDecimals = 2)
        {
            if (dgv == null) return;
            dgv.CellFormatting -= OnCellFormatting;
            dgv.CellFormatting += OnCellFormatting;

            void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.Value == null) return;
                decimal d;
                if (e.Value is decimal dec) d = dec;
                else if (e.Value is double dbl) d = (decimal)dbl;
                else if (e.Value is float fl) d = (decimal)fl;
                else if (e.Value is int or long)
                {
                    e.Value = e.Value.ToString();
                    e.FormattingApplied = true;
                    return;
                }
                else return;

                e.Value = maxDecimals <= 2 ? Money(d) : Quantity(d);
                e.FormattingApplied = true;
            }
        }
    }
}
