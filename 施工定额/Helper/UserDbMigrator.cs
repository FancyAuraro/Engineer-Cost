using Microsoft.Data.Sqlite;

namespace 施工定额.Helper
{
    /// <summary>
    /// 用户库版本化迁移。每个版本在独立事务中执行，成功后才推进 SchemaVersion。
    /// </summary>
    public static class UserDbMigrator
    {
        private const int CurrentVersion = 4;

        public static void Apply(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
                return;

            try
            {
                using var conn = new SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                conn.ExecuteNonQuery("PRAGMA busy_timeout=5000;");
                EnsureSchemaVersionTable(conn);
                var version = ReadVersion(conn);

                while (version < CurrentVersion)
                {
                    var nextVersion = version + 1;
                    using var tx = conn.BeginTransaction();
                    try
                    {
                        ApplyMigration(conn, tx, nextVersion);
                        WriteVersion(conn, tx, nextVersion);
                        tx.Commit();
                        version = nextVersion;
                        AppLogger.Info($"用户库迁移完成：v{version}");
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("用户库迁移失败", ex);
                throw;
            }
        }

        private static void EnsureSchemaVersionTable(SqliteConnection conn)
        {
            Execute(conn, null,
                "CREATE TABLE IF NOT EXISTS [SchemaVersion] ([Version] INTEGER NOT NULL);" +
                "INSERT INTO [SchemaVersion] ([Version]) " +
                "SELECT 0 WHERE NOT EXISTS (SELECT 1 FROM [SchemaVersion]);");
        }

        private static int ReadVersion(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MAX([Version]) FROM [SchemaVersion]";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        private static void WriteVersion(SqliteConnection conn, SqliteTransaction tx, int version)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE [SchemaVersion] SET [Version]=@version";
            cmd.Parameters.AddWithValue("@version", version);
            cmd.ExecuteNonQuery();
        }

        private static void ApplyMigration(SqliteConnection conn, SqliteTransaction tx, int version)
        {
            switch (version)
            {
                case 1:
                    EnsureColumn(conn, tx, "定额_市政工程", "换算系数", "REAL NOT NULL DEFAULT 1");
                    EnsureColumn(conn, tx, "清单", "项目类别", "TEXT NOT NULL DEFAULT '分部分项'");
                    break;
                case 2:
                    EnsureUnitProjectTable(conn, tx);
                    EnsureColumn(conn, tx, "清单", "单位工程编码", "TEXT NOT NULL DEFAULT 'DW001'");
                    BackfillUnitProjectCode(conn, tx);
                    break;
                case 3:
                    NormalizeQingdanCategory(conn, tx);
                    SeedOtherItemsInQingdan(conn, tx);
                    break;
                case 4:
                    EnsureIndexes(conn, tx);
                    break;
                default:
                    throw new InvalidOperationException($"未知用户库迁移版本：{version}");
            }
        }

        private static void NormalizeQingdanCategory(SqliteConnection conn, SqliteTransaction tx)
        {
            Execute(conn, tx,
                "UPDATE [清单] SET [项目类别] = CASE " +
                "WHEN [项目类别] IS NULL OR TRIM(CAST([项目类别] AS TEXT)) = '' THEN '分部分项' " +
                "WHEN CAST([项目类别] AS TEXT) IN ('0') THEN '分部分项' " +
                "WHEN CAST([项目类别] AS TEXT) IN ('1','2') THEN '措施项目' " +
                "WHEN CAST([项目类别] AS TEXT) IN ('3') THEN '其他项目' " +
                "WHEN CAST([项目类别] AS TEXT) IN ('分部分项','措施项目','其他项目') THEN CAST([项目类别] AS TEXT) " +
                "WHEN CAST([项目类别] AS TEXT) LIKE '%措施%' THEN '措施项目' " +
                "WHEN CAST([项目类别] AS TEXT) LIKE '%其他%' THEN '其他项目' " +
                "ELSE '分部分项' END;");
        }

        private static void SeedOtherItemsInQingdan(SqliteConnection conn, SqliteTransaction tx)
        {
            var seeds = new (string code, string name)[]
            {
                ("QT-ZJJE", "暂列金额"),
                ("QT-ZGJ", "暂估价"),
                ("QT-ZCB", "总承包服务费"),
                ("QT-JRG", "计日工"),
            };

            var dedupe =
                "DELETE FROM [清单] " +
                "WHERE [项目类别] = '其他项目' " +
                "AND rowid NOT IN (" +
                "SELECT MIN(rowid) FROM [清单] " +
                "WHERE [项目类别] = '其他项目' " +
                "GROUP BY [清单编码]);";
            var removed = Execute(conn, tx, dedupe);
            if (removed > 0)
                AppLogger.Info($"其他项目重复行已清理 {removed} 条");

            foreach (var (code, name) in seeds)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT INTO [清单] " +
                    "([清单编码], [清单名称], [项目特征], [单位], [工程量], [综合单价], [综合合价], [项目类别], [单位工程编码]) " +
                    "SELECT $c, $n, '', '', 0, 0, 0, '其他项目', 'DW001' " +
                    "WHERE NOT EXISTS (SELECT 1 FROM [清单] WHERE [清单编码] = $c AND [项目类别] = '其他项目');";
                cmd.Parameters.AddWithValue("$c", code);
                cmd.Parameters.AddWithValue("$n", name);
                cmd.ExecuteNonQuery();
            }

            Execute(conn, tx,
                "UPDATE [清单] SET [综合合价]=0 " +
                "WHERE [清单编码]='QT-ZGJ' AND [项目类别]='其他项目';");

            if (!TableExists(conn, tx, "其他项目"))
                return;

            foreach (var (code, name) in seeds)
            {
                using var select = conn.CreateCommand();
                select.Transaction = tx;
                select.CommandText = "SELECT [金额] FROM [其他项目] WHERE [名称]=$n";
                select.Parameters.AddWithValue("$n", name);
                var value = select.ExecuteScalar();
                if (value == null || value is DBNull) continue;

                var amount = code == "QT-ZGJ" ? 0m : Convert.ToDecimal(value);
                using var update = conn.CreateCommand();
                update.Transaction = tx;
                update.CommandText =
                    "UPDATE [清单] SET [综合合价]=$a " +
                    "WHERE [清单编码]=$c AND [项目类别]='其他项目'";
                update.Parameters.AddWithValue("$a", amount);
                update.Parameters.AddWithValue("$c", code);
                update.ExecuteNonQuery();
            }

            Execute(conn, tx, "DROP TABLE IF EXISTS [其他项目]");
            AppLogger.Info("已将旧「其他项目」表数据迁入清单并删除旧表");
        }

        private static void EnsureIndexes(SqliteConnection conn, SqliteTransaction tx)
        {
            EnsureIndex(conn, tx, "idx_定额_清单编码",
                "CREATE INDEX IF NOT EXISTS [idx_定额_清单编码] ON [定额_市政工程]([清单编码])");
            EnsureIndex(conn, tx, "idx_消耗量_清单编码",
                "CREATE INDEX IF NOT EXISTS [idx_消耗量_清单编码] ON [消耗量]([清单编码])");
            EnsureIndex(conn, tx, "idx_消耗量_编码",
                "CREATE INDEX IF NOT EXISTS [idx_消耗量_编码] ON [消耗量]([消耗量编码])");
            EnsureIndex(conn, tx, "idx_清单_项目类别",
                "CREATE INDEX IF NOT EXISTS [idx_清单_项目类别] ON [清单]([项目类别])");
            EnsureIndex(conn, tx, "idx_清单_单位工程编码",
                "CREATE INDEX IF NOT EXISTS [idx_清单_单位工程编码] ON [清单]([单位工程编码])");
            EnsureUniqueXiaohaoliangIndex(conn, tx);
        }

        private static void EnsureUniqueXiaohaoliangIndex(SqliteConnection conn, SqliteTransaction tx)
        {
            const string uniqueName = "uidx_消耗量_定额ID_编码";
            if (IndexExists(conn, tx, uniqueName)) return;

            var duplicateCount = ExecuteScalarInt(conn, tx,
                "SELECT COUNT(1) FROM (SELECT [定额ID], [消耗量编码] FROM [消耗量] " +
                "WHERE [定额ID] IS NOT NULL AND [消耗量编码] IS NOT NULL " +
                "GROUP BY [定额ID], [消耗量编码] HAVING COUNT(1) > 1)");
            if (duplicateCount > 0)
            {
                throw new InvalidOperationException(
                    $"用户库存在 {duplicateCount} 组重复消耗量记录，迁移已停止以避免自动删除用户数据。请先处理重复数据后重试。");
            }

            if (IndexExists(conn, tx, "idx_消耗量_定额ID_编码"))
                Execute(conn, tx, "DROP INDEX IF EXISTS [idx_消耗量_定额ID_编码]");

            Execute(conn, tx,
                $"CREATE UNIQUE INDEX IF NOT EXISTS [{uniqueName}] " +
                "ON [消耗量]([定额ID], [消耗量编码])");
        }

        private static void EnsureUnitProjectTable(SqliteConnection conn, SqliteTransaction tx)
        {
            Execute(conn, tx,
                "CREATE TABLE IF NOT EXISTS [单位工程] (" +
                "[编码] TEXT NOT NULL PRIMARY KEY, " +
                "[名称] TEXT NOT NULL, " +
                "[排序] INTEGER NOT NULL DEFAULT 0);" +
                "INSERT INTO [单位工程] ([编码], [名称], [排序]) " +
                "SELECT 'DW001', '默认单位工程', 0 " +
                "WHERE NOT EXISTS (SELECT 1 FROM [单位工程] WHERE [编码] = 'DW001');");
        }

        private static void BackfillUnitProjectCode(SqliteConnection conn, SqliteTransaction tx)
        {
            Execute(conn, tx,
                "UPDATE [清单] SET [单位工程编码]='DW001' " +
                "WHERE [单位工程编码] IS NULL OR TRIM(CAST([单位工程编码] AS TEXT))='';");
        }

        private static void EnsureColumn(SqliteConnection conn, SqliteTransaction tx, string table, string column, string typeSql)
        {
            using var check = conn.CreateCommand();
            check.Transaction = tx;
            check.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)})";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            reader.Close();
            Execute(conn, tx,
                $"ALTER TABLE {QuoteIdentifier(table)} ADD COLUMN {QuoteIdentifier(column)} {typeSql}");
            AppLogger.Info($"用户库已添加列 {table}.{column}");
        }

        private static void EnsureIndex(SqliteConnection conn, SqliteTransaction tx, string indexName, string createSql)
        {
            if (IndexExists(conn, tx, indexName)) return;
            Execute(conn, tx, createSql);
            AppLogger.Info($"用户库已创建索引 {indexName}");
        }

        private static bool IndexExists(SqliteConnection conn, SqliteTransaction tx, string indexName)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name=@name LIMIT 1";
            cmd.Parameters.AddWithValue("@name", indexName);
            return cmd.ExecuteScalar() != null;
        }

        private static bool TableExists(SqliteConnection conn, SqliteTransaction tx, string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1";
            cmd.Parameters.AddWithValue("@name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        private static int ExecuteScalarInt(SqliteConnection conn, SqliteTransaction tx, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        private static int Execute(SqliteConnection conn, SqliteTransaction? tx, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        private static string QuoteIdentifier(string value) =>
            "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    internal static class SqliteCommandExtensions
    {
        public static int ExecuteNonQuery(this SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }
    }
}
