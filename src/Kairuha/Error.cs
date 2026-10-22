/*
错误：表示用户能够通过修改请求解决的业务错误，并由入口转换成标准 Problem Details。
没有使用 Error 标记的异常属于程序自己的故障：开发环境返回原因，生产环境只返回定位编号。
调用示例：
  throw Error.Input("金额必须大于 0")
  throw Error.Missing("没有找到这笔账")
*/
using Microsoft.AspNetCore.Diagnostics;                     // 在 ASP.NET Core 入口统一处理异常
using Microsoft.AspNetCore.Http;                            // 写入 HTTP 状态和 Problem Details
using Microsoft.AspNetCore.Mvc;                             // 使用标准 ProblemDetails 数据结构
using Microsoft.Extensions.Hosting;                         // 决定程序故障能否显示具体原因
using Microsoft.Extensions.Logging;                         // 把程序故障和定位编号写进日志

namespace Kairuha;

/// <summary>
/// 用户能够自行修正的业务错误。
/// </summary>
public sealed class Error : Exception
{
    /// <summary>
    /// 这个错误对应的 HTTP 状态码。
    /// </summary>
    public int Status { get; }

    private Error(int status, string message) : base(message)
    {
        Status = status;                                    // 状态只在错误创建时确定，后面不允许改
    }

    /// <summary>
    /// 表示用户填写的内容不符合业务要求。
    /// </summary>
    public static Error Input(string message)
    {
        return new Error(StatusCodes.Status400BadRequest, message); // 用户修改输入后可以再次请求
    }

    /// <summary>
    /// 表示用户要找的业务数据不存在。
    /// </summary>
    public static Error Missing(string message)
    {
        return new Error(StatusCodes.Status404NotFound, message); // 路径存在，只是目标数据没有找到
    }

    /// <summary>
    /// 表示请求与当前业务状态冲突。
    /// </summary>
    public static Error Conflict(string message)
    {
        return new Error(StatusCodes.Status409Conflict, message); // 用户需要先刷新或改变当前状态
    }
}

// --- 把异常从同一个出口发回去 ---
internal sealed class ErrorHandler(
    IProblemDetailsService problems,
    IHostEnvironment environment,
    ILogger<ErrorHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return false;                                   // 客户端主动断开不是业务错误，交回服务器结束请求
        }

        var known = exception as Error;                      // 带状态码的是用户能够修正的业务错误
        var malformed = exception as BadHttpRequestException; // JSON 或字段类型无法解析同样是用户输入问题
        var status = known?.Status
            ?? malformed?.StatusCode
            ?? StatusCodes.Status500InternalServerError;    // 剩余异常才是程序故障
        var detail = known?.Message                          // 业务错误始终把可修改原因告诉用户
            ?? (malformed is not null
                ? "请求内容无法解析，请检查 JSON 和字段类型"
                : environment.IsDevelopment() ? exception.Message : "程序遇到了问题，请凭 traceId 查询日志");

        context.Response.StatusCode = status;                // HTTP 状态先确定，响应体会使用同一个值

        if (known is null && malformed is null)
        {
            log.LogError(exception, "程序处理请求时出错，traceId={TraceId}", context.TraceIdentifier); // 保存完整故障供定位
        }
        else
        {
            log.LogDebug("请求未完成：{Reason}，traceId={TraceId}", detail, context.TraceIdentifier); // 用户错误不污染错误日志
        }

        var problem = new ProblemDetails                     // 采用 ASP.NET Core 和客户端工具认识的标准结构
        {
            Status = status,
            Detail = detail,
        };

        await problems.WriteAsync(new ProblemDetailsContext  // 统一定制会补标题和 traceId
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });

        return true;                                         // 告诉 ASP.NET Core 这个异常已经完整处理
    }
}
