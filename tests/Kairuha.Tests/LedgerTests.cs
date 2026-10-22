/*
Ledger 验收：从真实 HTTP 入口验证记账、查询、汇总、删除和 OpenAPI。
每个测试使用自己的临时 SQLite 文件，不共享数据，也不会写进源码目录或用户数据目录。
运行示例：dotnet test --filter FullyQualifiedName~LedgerTests
*/
using System.Net;                                            // 核对每次请求的 HTTP 状态
using System.Net.Http.Json;                                  // 按接口的 JSON 结构发送和读取数据
using Microsoft.AspNetCore.Hosting;                          // 给进程内服务设置开发环境
using Microsoft.AspNetCore.Mvc.Testing;                      // 不占端口启动真实 Ledger 入口
using Microsoft.Extensions.Configuration;                   // 把数据库位置换成测试临时文件

namespace Kairuha.Tests;

public sealed class LedgerTests
{
    // --- 完整走过一笔账的生命周期 ---
    [Fact]
    public async Task Adds_lists_sums_and_removes_a_bill()
    {
        await using var app = new LedgerApp();               // 每次测试拿到一个全新的账本
        using var client = app.CreateClient();               // 请求经过和真实服务相同的 HTTP 管线

        var created = await client.PostAsJsonAsync("/bills", new // 记一笔 12.50 元的午饭
        {
            amount = 12.50m,
            category = " 吃饭 ",
            note = "午饭",
            date = new DateOnly(2026, 10, 22),
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode); // 新账单使用 201
        var bill = await created.Content.ReadFromJsonAsync<BillView>(); // 读回框架实际发出的账单
        Assert.NotNull(bill);                                    // 201 必须带回新建结果
        Assert.Equal(12.50m, bill.Amount);                        // 整数分正确换回元
        Assert.Equal("吃饭", bill.Category);                     // 主体指令已经整理首尾空格

        var listed = await client.GetFromJsonAsync<List<BillView>>("/bills?month=2026-10"); // 按月份查询
        var one = Assert.Single(Assert.IsType<List<BillView>>(listed)); // 新账本里只有刚才那一笔
        Assert.Equal(bill.Id, one.Id);                            // 查询和创建指向同一份数据

        var sum = await client.GetFromJsonAsync<SummaryView>("/sum?month=2026-10"); // 查看月度汇总
        Assert.NotNull(sum);                                      // 汇总接口必须返回内容
        Assert.Equal(12.50m, sum.Total);                           // 月总额没有浮点误差
        Assert.Equal(12.50m, sum.Categories["吃饭"]);             // 分类金额和总额来自同一批账单

        var removed = await client.DeleteAsync($"/bills/{bill.Id}"); // 删除刚创建的账单
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);       // 删除成功时返回被删内容

        var empty = await client.GetFromJsonAsync<List<BillView>>("/bills"); // 再看全部账单
        Assert.Empty(Assert.IsType<List<BillView>>(empty));        // 删除后唯一数据来源也已经为空
    }

    // --- OpenAPI 来自真实路由 ---
    [Fact]
    public async Task Publishes_the_actual_routes_in_development()
    {
        await using var app = new LedgerApp();               // OpenAPI 默认只在开发环境开放
        using var client = app.CreateClient();               // 从真实 HTTP 地址读取文档

        var document = await client.GetStringAsync("/openapi/v1.json"); // 获取框架生成的 OpenAPI

        Assert.Contains("\"/bills\"", document, StringComparison.Ordinal); // 包含账单创建和查询
        Assert.Contains("\"/bills/{id}\"", document, StringComparison.Ordinal); // 包含删除路由
        Assert.Contains("\"/sum\"", document, StringComparison.Ordinal); // 包含汇总路由
    }

    private sealed record BillView(                          // 测试只读取客户端真正看到的账单字段
        Guid Id,
        decimal Amount,
        string Category,
        string Note,
        DateOnly Date);

    private sealed record SummaryView(                       // 测试只读取客户端真正看到的汇总字段
        string Month,
        decimal Total,
        Dictionary<string, decimal> Categories);
}

// --- 给每个测试启动一个隔离的 Ledger ---
internal sealed class LedgerApp : WebApplicationFactory<Program>
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"kairuha-{Guid.NewGuid():N}.db"); // 这个实例独占的数据库

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");               // 开放 OpenAPI，并显示开发期故障信息
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?> // 在 Program 注册 Data 前换掉连接地址
            {
                ["ConnectionStrings:Ledger"] = $"Data Source={file}",
            });
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();                             // 先停止服务并释放 SQLite 连接
        if (File.Exists(file)) File.Delete(file);              // 只删除这个实例自己创建的临时账本
    }
}
