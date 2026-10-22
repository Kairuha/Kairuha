# Kairuha.Web

这是一个按 HOP 思想组织的 Kairuha 后端项目。

## 运行

```bash
dotnet run --urls http://localhost:5080
```

另开一个终端调用：

```bash
curl -X POST http://localhost:5080/greetings \
  -H 'Content-Type: application/json' \
  -d '{"name":"星绒"}'
```

返回：

```json
{"message":"你好，星绒！"}
```

损坏的 JSON、结构验证失败和显式业务错误都会使用带 `traceId` 的 RFC Problem Details。未知程序故障在生产环境不会把内部异常发给客户端。

开发环境的 OpenAPI 文档位于：

```text
http://localhost:5080/openapi/v1.json
```

## 从哪里继续写

```text
Program.cs    入口：哪条请求交给哪个业务指令
Greeting.cs   问候：问候的输入、规则和返回
```

增加一个新主体时，先创建以主体命名的文件，把它的操作写在一起，再在 `Program.cs` 添加直接路由。不要提前创建空的 Controller、Service、Repository 或 Validator 层。

`AddValidation()` 需要保留在 `Program.cs`：.NET 10 的验证源码生成器从应用程序集发现输入类型，框架不能替应用隐藏这个调用。认证、限流、数据库与其他中间件继续直接使用 ASP.NET Core 原生 API。
