# Kairuha

Kairuha 是一个按 HOP 思想设计、建立在 ASP.NET Core Minimal APIs 上的轻量后端框架。

它提供统一的 RFC Problem Details 错误处理与 OpenAPI 注册，同时保留原生路由、依赖注入、配置、日志和数据库能力。

```csharp
using Kairuha;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddKairuha();
builder.Services.AddValidation();

var app = builder.Build();
app.UseKairuha();
app.MapGet("/hello", () => "你好");
app.Run();
```

当前包是契约原型，公共 API 尚未稳定。
