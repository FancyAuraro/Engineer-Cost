using Dapper;
using Microsoft.Data.Sqlite;
using 施工定额.Entity;

namespace 施工定额
{
    public class UnitProjectRepository
    {
        private readonly string _connStr;

        public UnitProjectRepository(string connStr)
        {
            _connStr = connStr;
        }

        public void EnsureDefault()
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            conn.Execute(@"
CREATE TABLE IF NOT EXISTS 单位工程 (
  编码 TEXT PRIMARY KEY,
  名称 TEXT NOT NULL,
  排序 INTEGER NOT NULL DEFAULT 0
);
INSERT INTO 单位工程 (编码, 名称, 排序)
SELECT @Code, @Name, 0
WHERE NOT EXISTS (SELECT 1 FROM 单位工程 WHERE 编码 = @Code);",
                new { Code = UnitProject.DefaultCode, Name = "默认单位工程" });
        }

        public List<UnitProject> ListAll()
        {
            using var conn = new SqliteConnection(_connStr);
            return conn.Query<UnitProject>(
                "SELECT 编码, 名称, 排序 FROM 单位工程 ORDER BY 排序, 编码").ToList();
        }

        public UnitProject Add(string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("单位工程名称不能为空");

            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            var maxSort = conn.ExecuteScalar<int?>("SELECT MAX(排序) FROM 单位工程") ?? 0;
            var seq = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM 单位工程") + 1;
            string code;
            do
            {
                code = $"DW{seq:D3}";
                seq++;
            } while (conn.ExecuteScalar<int>("SELECT COUNT(1) FROM 单位工程 WHERE 编码=@c", new { c = code }) > 0);

            var item = new UnitProject { 编码 = code, 名称 = name, 排序 = maxSort + 1 };
            conn.Execute(
                @"INSERT INTO 单位工程 (编码, 名称, 排序) VALUES (@编码, @名称, @排序)",
                item);
            return item;
        }

        public void Rename(string code, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(newName))
                return;
            using var conn = new SqliteConnection(_connStr);
            conn.Execute("UPDATE 单位工程 SET 名称=@名称 WHERE 编码=@编码",
                new { 编码 = code, 名称 = newName });
        }

        public void Delete(string code)
        {
            if (string.IsNullOrEmpty(code))
                return;
            if (code == UnitProject.DefaultCode)
                throw new InvalidOperationException("不能删除默认单位工程。");

            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            // 先清理该单位下的其他项目（自动生成的默认项）
            conn.Execute(
                @"DELETE FROM 清单 WHERE 单位工程编码=@Code AND 项目类别=@Other",
                new { Code = code, Other = QingdanCategory.其他项目 });

            var cnt = conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM 清单 WHERE 单位工程编码=@Code", new { Code = code });
            if (cnt > 0)
                throw new InvalidOperationException($"该单位工程下还有 {cnt} 条清单，请先删除或移走清单后再删单位工程。");

            conn.Execute("DELETE FROM 单位工程 WHERE 编码=@Code", new { Code = code });
        }

        public int CountQingdan(string code)
        {
            using var conn = new SqliteConnection(_connStr);
            return conn.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM 清单 WHERE 单位工程编码=@Code AND IFNULL(项目类别,'') <> @Other",
                new { Code = code, Other = QingdanCategory.其他项目 });
        }
    }
}
