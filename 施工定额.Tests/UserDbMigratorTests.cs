using Microsoft.Data.Sqlite;
using 施工定额.Helper;
using Xunit;

namespace 施工定额.Tests
{
    public class UserDbMigratorTests
    {
        [Fact]
        public void Apply_UpgradesLegacyDatabaseAndRecordsCurrentVersion()
        {
            var path = Path.Combine(Path.GetTempPath(), $"EngineerCostMigration_{Guid.NewGuid():N}.db");
            try
            {
                using (var conn = new SqliteConnection($"Data Source={path}"))
                {
                    conn.Open();
                    Execute(conn, @"
CREATE TABLE 清单 (
    清单编码 TEXT NOT NULL PRIMARY KEY,
    清单名称 TEXT,
    项目特征 TEXT,
    单位 TEXT,
    工程量 REAL,
    综合单价 REAL,
    综合合价 REAL
);
CREATE TABLE 定额_市政工程 (
    ID号 TEXT NOT NULL PRIMARY KEY,
    清单编码 TEXT,
    定额编码 TEXT,
    定额名称 TEXT,
    定额单位 TEXT,
    定额工程量 REAL,
    定额单价 REAL,
    定额合价 REAL
);
CREATE TABLE 消耗量 (
    定额ID TEXT,
    清单编码 TEXT,
    定额编码 TEXT,
    消耗量类别 TEXT,
    消耗量编码 TEXT,
    消耗量名称 TEXT,
    规格型号 TEXT,
    消耗量单位 TEXT,
    含量 REAL,
    数量 REAL,
    定额基价 REAL,
    市场价 REAL,
    市场价合计 REAL
);
CREATE TABLE 其他项目 (名称 TEXT, 金额 REAL);
INSERT INTO 清单 (清单编码, 清单名称, 工程量, 综合合价) VALUES ('Q1', '旧数据', 2, 100);
INSERT INTO 其他项目 (名称, 金额) VALUES ('暂列金额', 12.34);");
                }

                UserDbMigrator.Apply(path);

                using var verify = new SqliteConnection($"Data Source={path}");
                verify.Open();
                Assert.Equal(4, Convert.ToInt32(Scalar(verify, "SELECT Version FROM SchemaVersion")));
                Assert.Equal(1, Convert.ToInt32(Scalar(verify, "SELECT COUNT(*) FROM \"单位工程\" WHERE \"编码\"='DW001'")));
                Assert.Equal("DW001", Scalar(verify, "SELECT \"单位工程编码\" FROM 清单 WHERE 清单编码='Q1'"));
                Assert.Equal(12.34m, Convert.ToDecimal(Scalar(verify, "SELECT \"综合合价\" FROM 清单 WHERE 清单编码='QT-ZJJE'")));
                Assert.Equal(0, Convert.ToInt32(Scalar(verify, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='其他项目'")));
                Assert.Equal(1, Convert.ToInt32(Scalar(verify, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='uidx_消耗量_定额ID_编码'")));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void Apply_DuplicateXiaohaoliang_DoesNotDeleteUserData()
        {
            var path = Path.Combine(Path.GetTempPath(), $"EngineerCostMigration_{Guid.NewGuid():N}.db");
            try
            {
                using (var conn = new SqliteConnection($"Data Source={path}"))
                {
                    conn.Open();
                    Execute(conn, @"
CREATE TABLE 清单 (
    清单编码 TEXT PRIMARY KEY,
    清单名称 TEXT,
    项目特征 TEXT,
    单位 TEXT,
    工程量 REAL,
    综合单价 REAL,
    综合合价 REAL
);
CREATE TABLE 定额_市政工程 (
    ID号 TEXT PRIMARY KEY,
    清单编码 TEXT
);
CREATE TABLE 消耗量 (
    定额ID TEXT,
    清单编码 TEXT,
    消耗量编码 TEXT,
    规格型号 TEXT
);
INSERT INTO 消耗量 (定额ID, 清单编码, 消耗量编码, 规格型号) VALUES ('D1', 'Q1', 'M1', 'A');
INSERT INTO 消耗量 (定额ID, 清单编码, 消耗量编码, 规格型号) VALUES ('D1', 'Q1', 'M1', 'B');");
                }

                var ex = Assert.Throws<InvalidOperationException>(() => UserDbMigrator.Apply(path));
                Assert.Contains("重复消耗量", ex.Message);

                using var verify = new SqliteConnection($"Data Source={path}");
                verify.Open();
                Assert.Equal(2, Convert.ToInt32(Scalar(verify, "SELECT COUNT(*) FROM 消耗量")));
                Assert.Equal(3, Convert.ToInt32(Scalar(verify, "SELECT Version FROM SchemaVersion")));
                Assert.Equal(0, Convert.ToInt32(Scalar(verify, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='uidx_消耗量_定额ID_编码'")));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static void Execute(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static object? Scalar(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteScalar();
        }
    }
}
