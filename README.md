# Engineer-Cost（施工定额）

工程计价 / 施工定额桌面工具（个人学习项目）。

> 代码仍在持续打磨中，欢迎提 Issue / PR。

## 功能概览

- 清单 / 定额 / 消耗量三层数据维护
- 人材机市场价全局同价、含量与工程量联动重算
- 管理费 / 利润 / 规费 / 增值税按 `appsettings.json` / 用户 `fee_settings.json` 取费
- 导入系统定额库条目到用户工程
- 河南省 YDJC（DBJ 41/T087-2024）成果导出
- 程序在线检查更新（工具栏手动触发）；定额库 `systemDB` 随程序包分发
- 帮助菜单：关于、打开日志/数据目录、从备份恢复用户库

## 技术栈

- .NET 8 Windows Forms
- SQLite + Dapper
- Microsoft.Extensions.Configuration

## 数据目录

| 数据 | 位置 |
|------|------|
| 用户库 `userDB.db` | `%AppData%\施工定额\`（首次运行从程序目录复制） |
| 用户库备份 | `%AppData%\施工定额\backups\`（按天，约保留 7 天） |
| 系统定额库 `systemDB.db` | 程序目录（随更新包分发） |
| 运行日志 | `%AppData%\施工定额\logs\` |

## 配置

`appsettings.json`：

- `ConnectionStrings`：用户库 / 系统库
- `FeeSettings`：管理费基数与费率、利润率、规费、增值税等
- `UpdateSettings:AppVersionInfoUrl`：程序版本检查 URL

用户可覆盖费率：`%AppData%\施工定额\fee_settings.json`。

## 开发

```bash
dotnet build 施工定额.sln
dotnet test 施工定额.sln
dotnet run --project 施工定额
```

## 架构说明（简要）

- **Form**：只处理 UI 事件与绑定
- **Presenter**：业务编排（改价 / 改量 / 重算 / 保存）
- **Service**：计算引擎、导入、导出、程序更新
- **Repository**：SQLite 持久化（接口抽象便于测试）
- **Entity**：清单 / 定额 / 消耗量 / 费用构成
- **AppComposition**：轻量组合根，集中创建 Repository / 计算服务 / 导入服务，避免窗体内散落 `new`

## License

个人学习用途。
<img width="1247" height="860" alt="image" src="https://github.com/user-attachments/assets/abb351e0-15ff-4a19-9b8e-96e9560a1171" />

