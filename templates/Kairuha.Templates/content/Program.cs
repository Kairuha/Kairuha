/*
入口：启动 HTTP 服务，把请求直接交给对应的业务主体，不在这里写业务判断。
运行：dotnet run --urls http://localhost:5080
调用示例：
  POST /greetings  内容是 {"name":"星绒"}
*/
using Kairuha;                                             // 接入统一错误和 OpenAPI

var builder = WebApplication.CreateSlimBuilder(args);      // 只启用后端 API 需要的 ASP.NET Core 能力
builder.Services.AddKairuha();                              // 统一处理业务错误、程序故障和空状态响应
builder.Services.AddValidation();                           // 必须在应用程序集调用，.NET 10 才能发现这里的输入类型

var app = builder.Build();                                  // 服务注册完成后构建原生 WebApplication
app.UseKairuha();                                           // 接住错误，开发环境开放 /openapi/v1.json

app.MapPost("/greetings", Greeting.Make);                   // 把问候请求直接交给问候主体

app.Run();                                                  // 开始接收 HTTP 请求

public partial class Program;                               // 让集成测试可以在进程内启动这个入口
