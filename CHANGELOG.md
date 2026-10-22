# 更新记录

Kairuha 在 `1.0` 前仍可能调整公共 API；破坏性变化会在这里给出迁移说明。

## 0.1.0-alpha.1（待发布）

第一版可运行的 HOP 契约原型：

- 基于 .NET 10 和 ASP.NET Core Minimal APIs。
- 提供 `AddKairuha()` 与 `UseKairuha()` 最小接入点。
- 使用 RFC Problem Details 统一业务错误、绑定错误、空状态响应和程序故障。
- 提供 `Error.Input`、`Error.Missing` 与 `Error.Conflict`。
- 开发环境从实际显式路由生成 OpenAPI。
- 提供 `dotnet new kairuha` 项目模板。
- 提供 EF Core SQLite Ledger 完整示例。
- HTTP 集成测试覆盖账单生命周期、验证、错误安全和 OpenAPI。
- 模板验收覆盖安装、生成、还原、构建、发布与真实 HTTP 请求。

当前版本尚未上传 NuGet。
