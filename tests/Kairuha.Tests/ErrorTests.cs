/*
错误验收：验证用户错误、外部格式错误和程序故障从同一个 Problem Details 出口返回。
业务错误必须保留可修改原因；生产环境的程序故障只给 traceId，不能泄露内部内容。
运行示例：dotnet test --filter FullyQualifiedName~ErrorTests
*/
using System.Net;                                            // 核对 400、404 和 500 状态
using System.Net.Http.Json;                                  // 读取标准 Problem Details
using System.Text;                                           // 构造故意损坏的 JSON 请求
using Microsoft.AspNetCore.Builder;                          // 建一个只用于错误验收的最小应用
using Microsoft.AspNetCore.Hosting;                          // 使用不占真实端口的 TestServer
using Microsoft.AspNetCore.Mvc;                              // 读取 Problem Details 标准字段
using Microsoft.AspNetCore.TestHost;                         // 在测试进程内发送真实 HTTP 请求
using Microsoft.Extensions.DependencyInjection;             // 注册 Kairuha 默认能力

namespace Kairuha.Tests;

public sealed class ErrorTests
{
    // --- 用户能够修正的错误保留原因 ---
    [Fact]
    public async Task Returns_business_reason_and_trace_id()
    {
        await using var app = new LedgerApp();               // 使用真实 Ledger 的业务输入检查
        using var client = app.CreateClient();               // 请求会经过 Kairuha 错误出口

        var response = await client.PostAsJsonAsync("/bills", new // 金额不符合业务规则
        {
            amount = -1,
            category = "吃饭",
            date = new DateOnly(2026, 10, 22),
        });
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); // 读取标准错误结构

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // 这是用户输入问题，不是程序故障
        Assert.NotNull(problem);                                      // 错误响应必须有正文
        Assert.Equal("金额必须大于 0", problem.Detail);              // 用户知道应该改什么
        Assert.True(problem.Extensions.ContainsKey("traceId"));       // 仍然可以和服务日志对应
    }

    // --- 损坏的 JSON 也是外部输入错误 ---
    [Fact]
    public async Task Returns_400_for_malformed_json()
    {
        await using var app = new LedgerApp();               // 使用真实 JSON 参数绑定
        using var client = app.CreateClient();               // 发送无法解析的正文
        using var content = new StringContent("{broken", Encoding.UTF8, "application/json"); // 故意少引号和结尾

        var response = await client.PostAsync("/bills", content); // 请求在进入 Bill.Add 前就会失败
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); // 错误仍走统一出口

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // 外部格式错误固定为 400
        Assert.Equal("请求内容无法解析，请检查 JSON 和字段类型", problem?.Detail); // 不把解析堆栈发给用户
    }

    // --- 应用程序集里的验证规则会在进入主体前执行 ---
    [Fact]
    public async Task Returns_validation_errors_for_endpoint_input()
    {
        await using var app = new LedgerApp();               // Ledger 在自己的程序集显式注册 AddValidation
        using var client = app.CreateClient();               // 请求会先经过 .NET 10 生成的验证代码

        var response = await client.PostAsJsonAsync("/bills", new // 分类违反 New 上的 Required 约束
        {
            amount = 1,
            category = "",
            date = new DateOnly(2026, 10, 22),
        });
        var text = await response.Content.ReadAsStringAsync(); // errors 扩展字段也属于公开错误契约

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // 结构错误固定为 400
        Assert.Contains("必须填写分类", text, StringComparison.Ordinal); // 保留输入类型声明的可读原因
        Assert.Contains("traceId", text, StringComparison.Ordinal);    // 自动验证和业务错误一样可定位
    }

    // --- 当前业务状态冲突使用 409 ---
    [Fact]
    public async Task Returns_409_for_business_conflict()
    {
        await using var app = await FaultApp("Production"); // 环境不应改变显式业务错误
        using var client = app.GetTestClient();               // 从真实异常处理中读取响应

        var response = await client.GetAsync("/conflict");   // 触发 Error.Conflict
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); // 读取标准错误结构

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // 冲突不是输入格式错误
        Assert.Equal("当前状态冲突", problem?.Title);               // 使用稳定的 409 标题
        Assert.Equal("这条记录已经存在", problem?.Detail);         // 用户能知道如何调整请求
    }

    // --- 找不到业务数据和找不到路由都保持 404 ---
    [Fact]
    public async Task Distinguishes_missing_data_from_missing_route()
    {
        await using var app = new LedgerApp();               // 新账本里没有任何记录
        using var client = app.CreateClient();               // 两个请求经过同一状态出口

        var dataResponse = await client.DeleteAsync("/bills/00000000-0000-0000-0000-000000000001"); // 路由存在、数据不存在
        var dataProblem = await dataResponse.Content.ReadFromJsonAsync<ProblemDetails>(); // 有具体业务原因
        var routeResponse = await client.GetAsync("/not-here"); // 整条路由不存在
        var routeProblem = await routeResponse.Content.ReadFromJsonAsync<ProblemDetails>(); // 由状态页补内容

        Assert.Equal(HttpStatusCode.NotFound, dataResponse.StatusCode); // 数据缺失是 404
        Assert.Equal("没有找到这笔账", dataProblem?.Detail);            // 主体给出具体原因
        Assert.Equal(HttpStatusCode.NotFound, routeResponse.StatusCode); // 路由缺失也是 404
        Assert.Equal("没有找到", routeProblem?.Title);                   // 空 404 也有可读标题
    }

    // --- 生产环境不泄露程序内部原因 ---
    [Fact]
    public async Task Hides_program_failure_in_production()
    {
        await using var app = await FaultApp("Production");  // 生产环境使用安全错误内容
        using var client = app.GetTestClient();               // TestServer 不占用系统端口

        var response = await client.GetAsync("/boom");       // 触发一个没有 Error 标记的异常
        var text = await response.Content.ReadAsStringAsync(); // 同时检查序列化后的全部内容
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); // 再检查标准字段

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // 程序故障固定为 500
        Assert.DoesNotContain("database password", text, StringComparison.Ordinal); // 内部原因不能发给客户端
        Assert.Contains("traceId", text, StringComparison.Ordinal);             // 客户端仍然拿得到定位编号
        Assert.Equal("程序出错", problem?.Title);                                // 标题不伪装成输入错误
    }

    // --- 开发环境保留程序故障原因 ---
    [Fact]
    public async Task Shows_program_failure_in_development()
    {
        await using var app = await FaultApp("Development"); // 本地开发需要立即看到真实原因
        using var client = app.GetTestClient();                // TestServer 不占用系统端口

        var response = await client.GetAsync("/boom");       // 触发同一个未标记异常
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(); // 读取开发期响应

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // 状态仍然是 500
        Assert.NotNull(problem);                                                // 500 必须使用标准错误正文
        Assert.Equal("database password is secret", problem.Detail);           // 开发环境保留原始异常原因
        Assert.True(problem.Extensions.ContainsKey("traceId"));                // 本地也使用同一定位方式
    }

    // --- 建一个只会抛错的最小应用 ---
    private static async Task<WebApplication> FaultApp(string environment)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions // 使用和真实模板相同的精简主机
        {
            EnvironmentName = environment,                   // 创建主机时就确定环境，所有注册服务看到同一个值
        });
        builder.WebHost.UseTestServer();                     // 请求留在测试进程内
        builder.Services.AddKairuha();                       // 只接入待验证的框架能力

        var app = builder.Build();                            // 完成服务注册
        app.UseKairuha();                                     // 启用统一异常出口
        app.MapGet("/boom", (Func<string>)(() => throw new InvalidOperationException("database password is secret"))); // 模拟内部故障
        app.MapGet("/conflict", (Func<string>)(() => throw Error.Conflict("这条记录已经存在"))); // 模拟业务状态冲突
        await app.StartAsync();                               // TestClient 需要应用已经启动
        return app;                                          // 测试结束时由调用方异步释放
    }
}
