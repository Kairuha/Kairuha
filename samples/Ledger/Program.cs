/*
入口：启动记账服务，把 HTTP 请求直接交给账单指令，不在这里做业务判断。
运行：dotnet run --project samples/Ledger
调用示例：
  POST   /bills                    记一笔账
  GET    /bills?month=2026-10      列出账单
  DELETE /bills/{id}               删除一笔账
  GET    /sum?month=2026-10        查看月度汇总
*/
using Kairuha;                                             // 接入统一错误和 OpenAPI

var builder = WebApplication.CreateSlimBuilder(args);      // 只启用后端 API 真正需要的 ASP.NET Core 能力
builder.Services.AddKairuha();                              // 统一处理业务错误、程序故障和空状态响应
builder.Services.AddValidation();                           // 在本程序集发现并检查路由输入类型
Data.Add(builder.Services);                                // 账单数据库的注册只由 Data 负责

var app = builder.Build();                                  // 服务注册完成后构建原生 WebApplication
await Data.Open(app.Services);                              // 启动时准备好数据库，后面读写不再反复判断
app.UseKairuha();                                           // 接住错误，开发环境开放 /openapi/v1.json

app.MapPost("/bills", Bill.Add);                            // 记一笔账
app.MapGet("/bills", Bill.List);                            // 列出全部账单或指定月份的账单
app.MapDelete("/bills/{id:guid}", Bill.Remove);             // 按编号删除一笔账
app.MapGet("/sum", Bill.Sum);                               // 汇总指定月份的总额和分类金额

app.Run();                                                  // 开始接收 HTTP 请求

public partial class Program;                               // 让集成测试可以在进程内启动这个入口
