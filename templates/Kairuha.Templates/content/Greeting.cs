/*
问候：接收一个名字，整理并生成一句问候语。这个主体的规则全部留在这里。
入口通过 POST /greetings 调用 Make。
调用示例：Greeting.Make(new Greeting.Input("星绒"))
*/
using System.ComponentModel.DataAnnotations;                // 声明 HTTP 请求必须提供名字
using Kairuha;                                              // 输入不符合业务规则时返回可读错误
using Microsoft.AspNetCore.Http.HttpResults;                // 返回能生成准确 OpenAPI 的类型化结果

public static class Greeting
{
    // --- 生成问候 ---
    public static Ok<Reply> Make(Input input)
    {
        var name = input.Name.Trim();                        // 先去掉用户不小心带上的首尾空格
        if (name.Length == 0) throw Error.Input("名字不能为空"); // 空格不能算作有效名字

        var reply = new Reply($"你好，{name}！");            // 业务结果只有这一句问候
        return TypedResults.Ok(reply);                       // 以明确的 200 结构交回客户端
    }

    public sealed record Input(                              // 请求结构同时用于 JSON、验证和 OpenAPI
        [Required(ErrorMessage = "必须填写名字")] string Name);

    public sealed record Reply(string Message);              // 返回结构只有客户端真正需要的内容
}
