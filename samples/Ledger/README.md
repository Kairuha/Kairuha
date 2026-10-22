# Ledger 示例

Ledger 是 Kairuha 的完整记账服务示例。它不是把代码按 Controller、Service、Repository 拆开，而是让一次请求保持这条直线：

```text
Program.cs 路由 → Bill.cs 完整业务 → Data.cs 唯一数据库
```

## 运行

在仓库根目录执行：

```bash
dotnet run --project samples/Ledger --urls http://localhost:5080
```

首次启动会在当前用户的本地应用数据目录创建 SQLite 数据库。也可以用标准配置覆盖连接字符串；测试正是用这个入口为每个用例创建独立数据库。

## 记一笔账

```bash
curl -i http://localhost:5080/bills \
  -H 'Content-Type: application/json' \
  -d '{"amount":12.50,"category":" 吃饭 ","note":"午饭","date":"2026-10-22"}'
```

接口返回 `201 Created`。金额对外使用“元”，内部保存整数“分”；分类两侧空格会在业务入口整理。

## 查询与汇总

```bash
curl 'http://localhost:5080/bills?month=2026-10'
curl 'http://localhost:5080/sum?month=2026-10'
```

列表和汇总共用 `Bill.cs` 中唯一的月份解释规则。月份格式错误会得到 `400` Problem Details，而不是服务器异常。

## 删除

把创建响应中的 `id` 放进路径：

```bash
curl -X DELETE http://localhost:5080/bills/{id}
```

编号不存在时得到 `404` 和“没有找到这笔账”；不存在的路由同样是 `404`，但不会伪装成业务数据错误。

## OpenAPI

开发环境默认提供实际路由生成的文档：

```text
http://localhost:5080/openapi/v1.json
```

生产环境默认不映射该地址，需要应用明确决定是否开放。

## 从哪里修改

| 需求 | 修改位置 |
|---|---|
| 增删接口路由 | `Program.cs` |
| 账单输入、规则、增删查和汇总 | `Bill.cs` |
| 数据库连接与账单表 | `Data.cs` |
| 框架级错误与 OpenAPI 行为 | `src/Kairuha/` |

例如“备注不能为空”只需要在 `Bill.Add` 整理 `note` 后增加业务判断，不需要新建 Validator、Service 或 Mapper。

`Data.Open` 使用 `EnsureCreated` 是为了让示例开箱即用，不是生产数据库升级方案。真实服务需要数据演进时，应直接采用 EF Core migrations 或适合该项目的迁移工具。

## 测试

```bash
dotnet test tests/Kairuha.Tests -c Release
```

测试会走真实 HTTP 管线，并覆盖创建、查询、汇总、删除、验证、非法 JSON、404、409、开发/生产 500 和 OpenAPI。
