/*
Kairuha 接入：把统一错误、空状态响应和开发环境 OpenAPI 接进 ASP.NET Core。
应用仍然直接使用 WebApplication 和原生 MapGet、MapPost 等方法，不经过 Kairuha 的路由包装。
调用示例：
  builder.Services.AddKairuha()
  var app = builder.Build()
  app.UseKairuha()
*/
using Microsoft.AspNetCore.Builder;                         // 使用 ASP.NET Core 的应用和中间件
using Microsoft.AspNetCore.Http;                            // 定制标准 Problem Details 错误内容
using Microsoft.Extensions.DependencyInjection;            // 向应用注册 Kairuha 所需能力
using Microsoft.Extensions.Hosting;                         // 判断当前是不是开发环境

namespace Kairuha;

/// <summary>
/// 把 Kairuha 的默认行为接入原生 ASP.NET Core 应用。
/// </summary>
public static class KairuhaApp
{
    /// <summary>
    /// 注册 Kairuha 的统一错误和 OpenAPI 能力。
    /// </summary>
    /// <param name="services">应用自己的服务集合。</param>
    /// <returns>原来的服务集合，可以继续注册数据库、认证等原生能力。</returns>
    public static IServiceCollection AddKairuha(this IServiceCollection services)
    {
        services.AddExceptionHandler<ErrorHandler>();       // 所有未处理错误都从同一个出口返回
        services.AddProblemDetails(options =>               // 使用 HTTP 标准错误结构，不再维护第二套格式
        {
            options.CustomizeProblemDetails = Describe;     // 给自动产生的 400、404、500 加上统一说明
        });
        services.AddOpenApi();                              // OpenAPI 直接从实际路由生成
        return services;                                    // 交回原集合，让应用继续按 ASP.NET Core 写法配置
    }

    /// <summary>
    /// 启用 Kairuha 的统一错误出口，并在开发环境开放 OpenAPI 文档。
    /// </summary>
    /// <param name="app">已经构建好的原生 WebApplication。</param>
    /// <returns>原来的应用，可以继续添加路由和中间件。</returns>
    public static WebApplication UseKairuha(this WebApplication app)
    {
        app.UseExceptionHandler();                           // 接住主体指令没有处理的业务错误和程序故障
        app.UseStatusCodePages();                            // 给空的 400、401、403、404 等响应补上可读内容

        if (app.Environment.IsDevelopment())                // 接口文档默认只在开发机器开放
        {
            app.MapOpenApi();                               // 地址是 /openapi/v1.json
        }

        return app;                                         // 交回原应用，路由仍然显式写在入口里
    }

    // --- 说明一个自动产生的 HTTP 错误 ---
    private static void Describe(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;                // ASP.NET Core 即将发给客户端的标准错误
        var status = problem.Status ?? context.HttpContext.Response.StatusCode; // 优先使用已经确定的状态码

        problem.Status = status;                             // 保证响应体和 HTTP 状态保持一致
        problem.Title = Title(status);                       // 用人能直接看懂的标题替换默认英文标题
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier; // 客户端可以拿编号去日志里定位

        if (status == StatusCodes.Status400BadRequest && string.IsNullOrWhiteSpace(problem.Detail))
        {
            problem.Detail = "请求内容不正确，请检查后重试"; // 没有更具体原因时也不能只返回一个空 400
        }
    }

    // --- 按状态码给错误起一个稳定的标题 ---
    private static string Title(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "请求内容不正确",
            StatusCodes.Status401Unauthorized => "需要登录",
            StatusCodes.Status403Forbidden => "没有权限",
            StatusCodes.Status404NotFound => "没有找到",
            StatusCodes.Status409Conflict => "当前状态冲突",
            StatusCodes.Status429TooManyRequests => "请求太频繁",
            >= StatusCodes.Status500InternalServerError => "程序出错",
            _ => "请求没有完成",
        };
    }
}
