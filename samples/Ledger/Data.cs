/*
数据：保存整个记账服务共用的数据位置和数据库配置，目前只有账单这一种数据。
真实运行的数据放在用户自己的应用数据目录；测试可以用 ConnectionStrings:Ledger 换到临时数据库。
调用示例：
  Data.Add(builder.Services)
  await Data.Open(app.Services)
*/
using Microsoft.EntityFrameworkCore;                       // 使用 SQLite 保存账单

public sealed class Data(DbContextOptions<Data> options) : DbContext(options)
{
    public DbSet<Bill.Item> Bills => Set<Bill.Item>();      // 账单表是账单主体的唯一持久化来源

    // --- 把数据库接进应用 ---
    public static void Add(IServiceCollection services)
    {
        services.AddDbContext<Data>((provider, options) =>   // 每个请求得到一个数据库会话
        {
            var configuration = provider.GetRequiredService<IConfiguration>(); // 创建会话时读取最终生效的配置
            var connection = configuration.GetConnectionString("Ledger") ?? DefaultConnection(); // 测试和部署可以改位置
            options.UseSqlite(connection);                   // 这个会话只连接确定好的账本
        });
    }

    // --- 启动时准备数据库 ---
    public static async Task Open(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope(); // 启动阶段单独创建一次请求范围
        var data = scope.ServiceProvider.GetRequiredService<Data>(); // 取出这一范围里的数据库
        await data.Database.EnsureCreatedAsync();             // 第一次运行建表，之后直接复用
    }

    // --- 决定真实账单存在哪里 ---
    private static string DefaultConnection()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); // 使用操作系统给当前用户的数据目录
        var folder = Path.Combine(home, "Kairuha", "Ledger"); // 升级或删除源码都不会带走用户账单
        Directory.CreateDirectory(folder);                    // 启动时确保目录存在，读写处不用再判断
        return $"Data Source={Path.Combine(folder, "ledger.db")}"; // SQLite 文件位置只有这里一个来源
    }
}
