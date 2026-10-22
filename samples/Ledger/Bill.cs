/*
账单：记账、列出、删除和月度汇总都在这里，数据通过 Data 的 Bills 表读写。
金额对外使用“元”，保存和汇总时使用整数“分”，不会出现二进制浮点小数误差。
调用示例：
  await Bill.List("2026-10", data)
  await Bill.Sum("2026-10", data)
*/
using System.ComponentModel.DataAnnotations;                // 声明外部请求必须带上的字段
using System.ComponentModel.DataAnnotations.Schema;         // 金额是从分推导的，不在数据库重复保存
using System.Globalization;                                 // 按固定的 yyyy-MM 规则理解月份
using System.Text.Json.Serialization;                       // 数据库里的“分”不重复发给客户端
using Kairuha;                                              // 用户输入不对时抛出可读业务错误
using Microsoft.AspNetCore.Http.HttpResults;                // 返回能生成准确 OpenAPI 的类型化结果
using Microsoft.EntityFrameworkCore;                        // 直接查询账单数据，不增加 Repository 转手层

public static class Bill
{
    // --- 记一笔账 ---
    public static async Task<Created<Item>> Add(New input, Data data)
    {
        var category = input.Category.Trim();                // 先整理用户输入，再判断是不是空的
        var note = input.Note?.Trim() ?? string.Empty;       // 备注可以不填，缺省时保存为空文字
        var cents = decimal.Round(input.Amount * 100, 0, MidpointRounding.AwayFromZero); // 先换成整数分再保存

        if (input.Amount <= 0) throw Error.Input("金额必须大于 0"); // 金额决定这是不是一笔有效支出
        if (cents / 100 != input.Amount) throw Error.Input("金额最多保留两位小数"); // 超过分的精度无法准确保存
        if (category.Length == 0) throw Error.Input("必须填写分类"); // 空格也不能当作分类
        if (input.Date == default) throw Error.Input("必须填写日期"); // 日期决定账单属于哪个月

        var bill = new Item(Guid.NewGuid(), decimal.ToInt64(cents), category, note, input.Date); // 组成完整账单
        data.Bills.Add(bill);                                // 写进账单唯一的数据表
        await data.SaveChangesAsync();                       // 确认数据库已经保存
        return TypedResults.Created($"/bills/{bill.Id}", bill); // 返回新账单和它自己的地址
    }

    // --- 列出账单 ---
    public static async Task<Ok<List<Item>>> List(string? month, Data data)
    {
        var bills = await Read(month, data);                 // 读取全部或指定月份，月份只在 Read 检查一次
        return TypedResults.Ok(bills);                       // 将账单按日期倒序交回客户端
    }

    // --- 删除一笔账 ---
    public static async Task<Ok<Item>> Remove(Guid id, Data data)
    {
        var bill = await data.Bills.FindAsync(id);           // 按路由里的编号找目标账单
        if (bill is null) throw Error.Missing("没有找到这笔账"); // 路径存在，只是这条业务数据不存在

        data.Bills.Remove(bill);                             // 从唯一的账单表中删除
        await data.SaveChangesAsync();                       // 确认删除已经落盘
        return TypedResults.Ok(bill);                        // 返回被删内容，用户能确认删的是哪一笔
    }

    // --- 汇总一个月的账单 ---
    public static async Task<Ok<Summary>> Sum(string? month, Data data)
    {
        if (string.IsNullOrWhiteSpace(month)) throw Error.Input("汇总必须指定月份，例如 month=2026-10"); // 汇总没有默认月份
        month = month.Trim();                                // 去掉问号参数两边不小心带上的空格

        var bills = await Read(month, data);                 // 和列表共用同一份月份检查和查询规则
        var cents = new Dictionary<string, long>();          // 每个分类先累计整数分

        foreach (var bill in bills)                          // 一笔一笔加入自己的分类
        {
            cents[bill.Category] = cents.GetValueOrDefault(bill.Category) + bill.Cents; // 同类支出加在一起
        }

        var categories = cents.ToDictionary(                // 返回前才把分换回元
            pair => pair.Key,
            pair => pair.Value / 100m);
        var total = cents.Values.Sum() / 100m;               // 所有分类的分加起来就是月度总支出

        return TypedResults.Ok(new Summary(month, total, categories)); // 一次返回月份、总额和分类明细
    }

    // --- 读取全部或指定月份的账单 ---
    private static async Task<List<Item>> Read(string? month, Data data)
    {
        var query = data.Bills.AsNoTracking();               // 查询只读数据，不保留无用的修改跟踪

        if (month is not null)                               // 没传月份时列出全部账单
        {
            month = month.Trim();                            // 先整理问号参数
            var valid = DateOnly.TryParseExact(              // 月份必须严格写成四位年和两位月
                $"{month}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var start);
            if (!valid) throw Error.Input("月份要写成 2026-10 这样"); // 告诉用户可以直接照抄的格式

            var end = start.AddMonths(1);                    // 下个月第一天是当前月份的结束边界
            query = query.Where(bill => bill.Date >= start && bill.Date < end); // 只保留这个月的账单
        }

        return await query.OrderByDescending(bill => bill.Date).ThenByDescending(bill => bill.Id).ToListAsync(); // 结果顺序稳定可预测
    }

    public sealed record New(                               // HTTP 请求只需要用户能够填写的四项
        decimal Amount,
        [Required(ErrorMessage = "必须填写分类")] string Category,
        string? Note,
        DateOnly Date);

    public sealed class Item                                 // 数据库和 HTTP 返回共用这一份账单定义
    {
        private Item() { }                                   // EF Core 从数据库恢复数据时使用

        public Item(Guid id, long cents, string category, string note, DateOnly date)
        {
            Id = id;                                         // 每笔账的唯一编号
            Cents = cents;                                   // 数据库只保存整数分
            Category = category;                             // 汇总使用的分类
            Note = note;                                     // 用户留下的说明
            Date = date;                                     // 账单归属日期
        }

        public Guid Id { get; private set; }                  // 路由删除账单时使用的编号

        [JsonIgnore]
        public long Cents { get; private set; }               // 内部精确金额，不和 Amount 重复发给客户端

        [NotMapped]
        public decimal Amount => Cents / 100m;                // 对外仍然使用人熟悉的元

        public string Category { get; private set; } = string.Empty; // 账单分类
        public string Note { get; private set; } = string.Empty;     // 可选备注
        public DateOnly Date { get; private set; }            // 账单日期
    }

    public sealed record Summary(                            // 月度汇总只有一份稳定返回结构
        string Month,
        decimal Total,
        IReadOnlyDictionary<string, decimal> Categories);
}
