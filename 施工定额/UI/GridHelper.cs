using System.ComponentModel;
using System.Reflection;

namespace 施工定额.UI
{
    public class ColumnConfig
    {
        public string FieldName { get; set; } = "";
        public string HeaderText { get; set; } = "";
        public int Width { get; set; } = 100;
        public bool ReadOnly { get; set; } = true;
        public string Format { get; set; } = "";
        public DataGridViewContentAlignment Alignment { get; set; }
            = DataGridViewContentAlignment.MiddleLeft;
        public bool WrapMode { get; set; } = false;
    }

    public static class GridManager
    {
        /// <summary>兼容旧引用，等同 UiTheme.Font。</summary>
        public static Font UiFont => UiTheme.Font;
        public static Font UiHeaderFont => UiTheme.HeaderFont;

        public static void BindOnce<T>(DataGridView dgv,
                                        BindingList<T> bindingList,
                                        List<ColumnConfig> columns)
        {
            dgv.SuspendLayout();
            try
            {
                dgv.AutoGenerateColumns = false;
                dgv.AllowUserToAddRows = false;
                dgv.AllowUserToDeleteRows = false;
                dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dgv.RowHeadersVisible = false;

                typeof(DataGridView)
                    .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dgv, true);

                dgv.Font = UiTheme.Font;
                dgv.DefaultCellStyle.Font = UiTheme.Font;
                dgv.ColumnHeadersDefaultCellStyle.Font = UiTheme.HeaderFont;
                dgv.RowTemplate.Height = 28;

                if (dgv.Columns.Count == 0)
                {
                    foreach (var col in columns)
                    {
                        dgv.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            Name = col.FieldName,
                            DataPropertyName = col.FieldName,
                            HeaderText = col.HeaderText,
                            Width = col.Width,
                            ReadOnly = col.ReadOnly,
                            DefaultCellStyle = new DataGridViewCellStyle
                            {
                                Format = col.Format,
                                Alignment = col.Alignment,
                                Font = UiTheme.Font,
                                WrapMode = col.WrapMode
                                    ? DataGridViewTriState.True
                                    : DataGridViewTriState.NotSet
                            }
                        });
                    }
                }

                dgv.DataSource = bindingList;
                dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
                dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                if (dgv.Columns.Contains("项目特征"))
                {
                    var feat = dgv.Columns["项目特征"]!;
                    feat.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    feat.DefaultCellStyle.Font = UiTheme.Font;
                }

                dgv.EditingControlShowing -= Grid_EditingControlShowing_Multiline;
                dgv.EditingControlShowing += Grid_EditingControlShowing_Multiline;

                // 恢复并记忆用户调整的列宽
                GridLayoutStore.Attach(dgv);
            }
            finally
            {
                dgv.ResumeLayout();
            }
        }

        private static bool IsFeatureColumn(DataGridViewColumn col) =>
            col.DefaultCellStyle.WrapMode == DataGridViewTriState.True
            || string.Equals(col.Name, "项目特征", StringComparison.Ordinal)
            || string.Equals(col.DataPropertyName, "项目特征", StringComparison.Ordinal);

        private static void Grid_EditingControlShowing_Multiline(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (sender is not DataGridView dgv) return;
            if (dgv.CurrentCell == null) return;
            var col = dgv.Columns[dgv.CurrentCell.ColumnIndex];

            if (e.Control is TextBox tb)
            {
                tb.Font = UiTheme.Font;
                if (IsFeatureColumn(col))
                {
                    tb.Multiline = true;
                    tb.AcceptsReturn = true;
                    tb.WordWrap = true;
                    tb.ScrollBars = ScrollBars.Vertical;
                }
            }
        }
    }

    public static class GridColumns
    {
        public static List<ColumnConfig> Qingdan => new List<ColumnConfig>
        {
            new() { FieldName = "清单编码",   HeaderText = "清单编码",   Width = 120 },
            new() { FieldName = "清单名称",   HeaderText = "清单名称",   Width = 200, ReadOnly = false },
            new() { FieldName = "项目特征",   HeaderText = "项目特征",   Width = 250, WrapMode = true, ReadOnly = false },
            new() { FieldName = "单位",       HeaderText = "单位",       Width = 60  },
            new() { FieldName = "工程量",     HeaderText = "工程量",     Width = 100,
                    ReadOnly = false, Format = "0.####",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "综合单价",   HeaderText = "综合单价",   Width = 100,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "综合合价",   HeaderText = "综合合价",   Width = 120,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
        };

        public static List<ColumnConfig> MeasureQingdan => new List<ColumnConfig>
        {
            new() { FieldName = "项目类别",     HeaderText = "类别",       Width = 90 },
            new() { FieldName = "清单编码",     HeaderText = "清单编码",   Width = 120 },
            new() { FieldName = "清单名称",     HeaderText = "清单名称",   Width = 200, ReadOnly = false },
            new() { FieldName = "项目特征",     HeaderText = "项目特征",   Width = 250, WrapMode = true, ReadOnly = false },
            new() { FieldName = "单位",         HeaderText = "单位",       Width = 60  },
            new() { FieldName = "工程量",       HeaderText = "工程量",     Width = 100,
                    ReadOnly = false, Format = "0.####",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "综合单价",     HeaderText = "综合单价",   Width = 100,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "综合合价",     HeaderText = "综合合价",   Width = 120,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
        };

        public static List<ColumnConfig> Dinge => new List<ColumnConfig>
        {
            new() { FieldName = "ID号",       HeaderText = "ID号", Width = 1, ReadOnly = true },
            new() { FieldName = "定额编码",   HeaderText = "定额编码",   Width = 120 },
            new() { FieldName = "定额名称",   HeaderText = "定额名称",   Width = 200, ReadOnly = false },
            new() { FieldName = "定额单位",   HeaderText = "单位",       Width = 60  },
            new() { FieldName = "换算系数",   HeaderText = "换算系数",   Width = 80,
                    ReadOnly = false, Format = "0.####",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "定额工程量", HeaderText = "工程量",     Width = 100,
                    ReadOnly = false, Format = "0.####",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "定额单价",   HeaderText = "单价",       Width = 100,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "定额合价",   HeaderText = "合价",       Width = 120,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
        };

        public static List<ColumnConfig> Xiaohaoliang => new List<ColumnConfig>
        {
            new() { FieldName = "消耗量类别", HeaderText = "类别",   Width = 60 },
            new() { FieldName = "消耗量编码", HeaderText = "编码",   Width = 100 },
            new() { FieldName = "消耗量名称", HeaderText = "名称",   Width = 160 },
            new() { FieldName = "规格型号",   HeaderText = "规格",   Width = 100 },
            new() { FieldName = "消耗量单位", HeaderText = "单位",   Width = 60 },
            new() { FieldName = "含量",       HeaderText = "含量",   Width = 80,
                    ReadOnly = false, Format = "0.####",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "数量",       HeaderText = "数量",   Width = 80,
                    Format = "0.####", Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "市场价",     HeaderText = "市场价", Width = 90,
                    ReadOnly = false, Format = "0.##",
                    Alignment = DataGridViewContentAlignment.MiddleRight },
            new() { FieldName = "市场价合计", HeaderText = "合价",   Width = 100,
                    Format = "0.##", Alignment = DataGridViewContentAlignment.MiddleRight },
        };
    }
}
