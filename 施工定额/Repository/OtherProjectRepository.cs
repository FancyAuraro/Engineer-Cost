using Dapper;
using Microsoft.Data.Sqlite;
using 施工定额.Entity;

namespace 施工定额
{
    /// <summary>
    /// 其他项目读写：数据落在「清单」表，项目类别=其他项目，按单位工程隔离。
    /// 默认单位工程(DW001)沿用 QT-ZJJE 等编码；其他单位工程使用 QT-ZJJE_DW00x。
    /// </summary>
    public class OtherProjectRepository
    {
        private readonly string _connStr;

        public static readonly (string Code, string Name)[] Defaults =
        {
            ("QT-ZJJE", "暂列金额"),
            ("QT-ZGJ", "暂估价"),
            ("QT-ZCB", "总承包服务费"),
            ("QT-JRG", "计日工"),
        };

        public OtherProjectRepository(string connStr)
        {
            _connStr = connStr;
        }

        /// <summary>默认单位工程保持原编码；其他单位：{base}_{unitCode}。</summary>
        public static string MakeItemCode(string baseCode, string unitCode)
        {
            unitCode = string.IsNullOrWhiteSpace(unitCode) ? UnitProject.DefaultCode : unitCode.Trim();
            if (string.Equals(unitCode, UnitProject.DefaultCode, StringComparison.Ordinal))
                return baseCode;
            return baseCode + "_" + unitCode;
        }

        public static bool IsZgjCode(string? code) =>
            !string.IsNullOrEmpty(code)
            && (code == "QT-ZGJ"
                || code.StartsWith("QT-ZGJ_", StringComparison.Ordinal));

        public static bool IsDefaultBaseCode(string? code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            foreach (var (baseCode, _) in Defaults)
            {
                if (code == baseCode || code.StartsWith(baseCode + "_", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool IsDefaultCode(string? code) => IsDefaultBaseCode(code);

        /// <summary>加载指定单位工程的其他项目；若无则写入四条默认项。</summary>
        public List<OtherProjectItem> LoadOrSeed(string? unitProjectCode = null)
        {
            var unit = string.IsNullOrWhiteSpace(unitProjectCode)
                ? UnitProject.DefaultCode
                : unitProjectCode.Trim();

            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            MigrateFromLegacyTable(conn);
            SeedDefaultsForUnit(conn, unit);

            var rows = conn.Query<(string 清单编码, string 清单名称, decimal 综合合价)>(
                @"SELECT 清单编码, 清单名称, IFNULL(综合合价,0) AS 综合合价
                  FROM 清单
                  WHERE 项目类别 = @Cat AND 单位工程编码 = @Unit
                  ORDER BY 清单编码",
                new { Cat = QingdanCategory.其他项目, Unit = unit }).ToList();

            // 默认四项按固定顺序排在前面
            rows = rows
                .OrderBy(r => DefaultSortKey(r.清单编码))
                .ThenBy(r => r.清单编码)
                .ToList();

            return rows.Select(r => new OtherProjectItem
            {
                清单编码 = r.清单编码,
                名称 = r.清单名称,
                金额 = IsZgjCode(r.清单编码) ? 0 : r.综合合价
            }).ToList();
        }

        /// <summary>仅为指定单位工程写入默认其他项目（已存在则跳过）。</summary>
        public void EnsureDefaultsForUnit(string unitProjectCode)
        {
            var unit = string.IsNullOrWhiteSpace(unitProjectCode)
                ? UnitProject.DefaultCode
                : unitProjectCode.Trim();
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            SeedDefaultsForUnit(conn, unit);
        }

        public void SaveAmount(string code, decimal amount, string? unitProjectCode = null)
        {
            if (IsZgjCode(code) || code == "暂估价")
                amount = 0;

            var unit = string.IsNullOrWhiteSpace(unitProjectCode) ? null : unitProjectCode.Trim();
            using var conn = new SqliteConnection(_connStr);
            if (unit == null)
            {
                conn.Execute(
                    @"UPDATE 清单 SET 综合合价=@金额, 综合单价=0, 工程量=0
                      WHERE 项目类别=@Cat AND (清单编码=@Key OR 清单名称=@Key)",
                    new { 金额 = amount, Cat = QingdanCategory.其他项目, Key = code });
            }
            else
            {
                conn.Execute(
                    @"UPDATE 清单 SET 综合合价=@金额, 综合单价=0, 工程量=0
                      WHERE 项目类别=@Cat AND 单位工程编码=@Unit
                        AND (清单编码=@Key OR 清单名称=@Key)",
                    new { 金额 = amount, Cat = QingdanCategory.其他项目, Key = code, Unit = unit });
            }
        }

        public void SaveName(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code) || IsZgjCode(code))
                return;
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                return;

            using var conn = new SqliteConnection(_connStr);
            conn.Execute(
                @"UPDATE 清单 SET 清单名称=@Name
                  WHERE 清单编码=@Code AND 项目类别=@Cat",
                new { Name = name, Code = code, Cat = QingdanCategory.其他项目 });
        }

        public OtherProjectItem AddCustom(string name, decimal amount = 0, string? unitProjectCode = null)
        {
            name = string.IsNullOrWhiteSpace(name) ? "新增项目" : name.Trim();
            var unit = string.IsNullOrWhiteSpace(unitProjectCode)
                ? UnitProject.DefaultCode
                : unitProjectCode.Trim();

            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            string code = AllocateCustomCode(conn, unit);
            conn.Execute(
                @"INSERT INTO 清单
                    (清单编码, 清单名称, 项目特征, 单位, 工程量, 综合单价, 综合合价, 项目类别, 单位工程编码)
                  VALUES
                    (@Code, @Name, '', '', 0, 0, @Amt, @Cat, @Unit)",
                new { Code = code, Name = name, Amt = amount, Cat = QingdanCategory.其他项目, Unit = unit });
            return new OtherProjectItem { 清单编码 = code, 名称 = name, 金额 = amount };
        }

        public bool Delete(string code)
        {
            if (string.IsNullOrEmpty(code) || IsZgjCode(code))
                return false;
            using var conn = new SqliteConnection(_connStr);
            var n = conn.Execute(
                @"DELETE FROM 清单 WHERE 清单编码=@Code AND 项目类别=@Cat",
                new { Code = code, Cat = QingdanCategory.其他项目 });
            return n > 0;
        }

        /// <summary>删除某单位工程下全部其他项目（删单位工程前调用）。</summary>
        public int DeleteAllForUnit(string unitProjectCode)
        {
            if (string.IsNullOrWhiteSpace(unitProjectCode))
                return 0;
            using var conn = new SqliteConnection(_connStr);
            return conn.Execute(
                @"DELETE FROM 清单 WHERE 项目类别=@Cat AND 单位工程编码=@Unit",
                new { Cat = QingdanCategory.其他项目, Unit = unitProjectCode.Trim() });
        }

        private static int DefaultSortKey(string code)
        {
            foreach (var (i, (baseCode, _)) in Defaults.Select((d, i) => (i, d)))
            {
                if (code == baseCode || code.StartsWith(baseCode + "_", StringComparison.Ordinal))
                    return i;
            }
            return 50;
        }

        private static void SeedDefaultsForUnit(SqliteConnection conn, string unitCode)
        {
            foreach (var (baseCode, name) in Defaults)
            {
                string code = MakeItemCode(baseCode, unitCode);
                conn.Execute(
                    @"INSERT INTO 清单
                        (清单编码, 清单名称, 项目特征, 单位, 工程量, 综合单价, 综合合价, 项目类别, 单位工程编码)
                      SELECT @Code, @Name, '', '', 0, 0, 0, @Cat, @Unit
                      WHERE NOT EXISTS (
                        SELECT 1 FROM 清单
                        WHERE 项目类别 = @Cat AND 单位工程编码 = @Unit
                          AND (清单编码 = @Code OR 清单名称 = @Name)
                      )",
                    new { Code = code, Name = name, Cat = QingdanCategory.其他项目, Unit = unitCode });
            }

            // 暂估价强制为 0
            string zgj = MakeItemCode("QT-ZGJ", unitCode);
            conn.Execute(
                @"UPDATE 清单 SET 综合合价=0
                  WHERE 清单编码=@Code AND 项目类别=@Cat AND 单位工程编码=@Unit",
                new { Code = zgj, Cat = QingdanCategory.其他项目, Unit = unitCode });
        }

        private static string AllocateCustomCode(SqliteConnection conn, string unitCode)
        {
            var codes = conn.Query<string>(
                @"SELECT 清单编码 FROM 清单
                  WHERE 项目类别=@Cat AND 清单编码 LIKE 'QT-C%'",
                new { Cat = QingdanCategory.其他项目 }).ToList();
            int max = 0;
            foreach (var c in codes)
            {
                if (c == null) continue;
                // QT-C001 or QT-C001_DW002
                var core = c;
                int at = core.IndexOf('_');
                if (at > 0) core = core.Substring(0, at);
                if (core.StartsWith("QT-C", StringComparison.Ordinal)
                    && int.TryParse(core.AsSpan(4), out var n) && n > max)
                    max = n;
            }
            for (int i = 1; i < 10000; i++)
            {
                string baseCandidate = "QT-C" + (max + i).ToString("D3");
                string candidate = MakeItemCode(baseCandidate, unitCode);
                var hit = conn.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM 清单 WHERE 清单编码=@C", new { C = candidate });
                if (hit == 0) return candidate;
            }
            throw new InvalidOperationException("无法分配其他项目编码");
        }

        private static void MigrateFromLegacyTable(SqliteConnection conn)
        {
            var exists = conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='其他项目'");
            if (exists == 0) return;

            foreach (var (code, name) in Defaults)
            {
                var amount = conn.ExecuteScalar<decimal?>(
                    "SELECT 金额 FROM 其他项目 WHERE 名称=@Name", new { Name = name });
                if (amount == null) continue;
                if (code == "QT-ZGJ") amount = 0;
                conn.Execute(
                    @"UPDATE 清单 SET 综合合价=@Amt
                      WHERE 清单编码=@Code AND 项目类别=@Cat",
                    new { Amt = amount.Value, Code = code, Cat = QingdanCategory.其他项目 });
            }

            conn.Execute("DROP TABLE IF EXISTS \"其他项目\"");
        }
    }
}
