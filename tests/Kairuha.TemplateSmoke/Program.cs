/*
模板验收：从刚打出的本地包创建一个全新项目，再构建、发布、启动并请求它。
这条路径故意不引用 Kairuha 源码，确保模板没有依赖开发仓库里的隐含状态。
调用示例：
  dotnet run --project tests/Kairuha.TemplateSmoke -- artifacts/packages
*/
using System.Diagnostics;                                  // 启动隔离的 dotnet 命令和发布产物
using System.IO.Compression;                                // 检查 NuGet 包里没有构建机路径或私密文件
using System.Net;                                           // 核对真实 HTTP 状态码
using System.Net.Http.Json;                                 // 按生成项目的 JSON 契约发送请求
using System.Net.Sockets;                                   // 为临时服务选择一个当前空闲端口
using System.Text;                                          // 构造故意损坏的 JSON
using System.Text.Json;                                     // 检查 Problem Details 与 OpenAPI
using System.Xml.Linq;                                      // 生成跨平台且隔离的 NuGet 源配置

if (args.Length != 1)
{
    Console.Error.WriteLine("用法：Kairuha.TemplateSmoke <包含两个 nupkg 的目录>");
    return 2;
}

var packages = Path.GetFullPath(args[0]);                   // 后续子进程都使用不依赖工作目录的绝对路径
if (!Directory.Exists(packages)) throw new DirectoryNotFoundException($"没有找到包目录：{packages}");

var templatePackages = Directory.GetFiles(packages, "Kairuha.Templates.*.nupkg"); // 每次验收只能对应一个模板版本
if (templatePackages.Length != 1) throw new InvalidOperationException($"预期一个模板包，实际找到 {templatePackages.Length} 个");
var runtimePackages = Directory.GetFiles(packages, "Kairuha.*.nupkg") // 模板会从同一目录还原唯一的运行包
    .Where(path => !Path.GetFileName(path).StartsWith("Kairuha.Templates.", StringComparison.Ordinal))
    .ToArray();
if (runtimePackages.Length != 1) throw new InvalidOperationException($"预期一个运行包，实际找到 {runtimePackages.Length} 个");
VerifyPackages([runtimePackages[0], templatePackages[0]]);  // 在执行模板前先拒绝带本机路径或私密文件的包

var root = Path.Combine(Path.GetTempPath(), $"kairuha-template-smoke-{Guid.NewGuid():N}"); // 本次验收独占的临时根目录
var hive = Path.Combine(root, "hive");                      // 不污染用户已经安装的 dotnet new 模板
var app = Path.Combine(root, "TemplateSmoke");             // 模拟用户从空目录创建的新项目
var publish = Path.Combine(root, "publish");               // 只运行发布产物，不借用 build 输出
var nugetConfig = Path.Combine(root, "NuGet.config");       // 不继承构建机可能存在的私有包源
var restoredPackages = Path.Combine(root, "packages");     // 不让全局缓存掩盖待验收的本地运行包
Directory.CreateDirectory(hive);
WriteNuGetConfig(nugetConfig, packages, restoredPackages);

var succeeded = false;
try
{
    await Dotnet(root, "new", "--debug:custom-hive", hive, "install", templatePackages[0], "--force"); // 安装刚打出的模板包
    await Dotnet(root, "new", "--debug:custom-hive", hive, "kairuha", "-n", "TemplateSmoke", "-o", app, "--no-restore"); // 从空目录生成项目

    var project = Path.Combine(app, "TemplateSmoke.csproj"); // sourceName 必须把项目文件一起改名
    if (!File.Exists(project)) throw new InvalidOperationException("模板没有生成 TemplateSmoke.csproj");
    if (Directory.Exists(Path.Combine(app, "content"))) throw new InvalidOperationException("模板错误地多生成了一层 content 目录");

    var compatibility = CompatibilityArgument();             // 仅供缺少 Targeting Pack 的本地发行版临时使用
    await Dotnet(app, Arguments(
        ["restore", project, "--configfile", nugetConfig],
        compatibility));
    await Dotnet(app, Arguments(["build", project, "-c", "Release", "--no-restore"], compatibility));
    await Dotnet(app, Arguments(["publish", project, "-c", "Release", "--no-build", "--no-restore", "-o", publish], compatibility));

    await VerifyHttp(publish);                               // 最终判断来自真实发布服务，不只看命令退出码
    succeeded = true;
    Console.WriteLine("模板验收通过：install → new → restore → build → publish → HTTP");
    return 0;
}
finally
{
    if (succeeded)
    {
        TryDelete(root);                                    // 只删除本次用 GUID 创建的临时目录
    }
    else
    {
        Console.Error.WriteLine($"模板验收目录已保留，便于定位：{root}"); // 失败现场比自动清理更有用
    }
}

// --- 为生成项目创建不依赖宿主机器的 NuGet 配置 ---
static void WriteNuGetConfig(string path, string localSource, string globalPackagesFolder)
{
    var document = new XDocument(
        new XElement("configuration",
            new XElement("config",
                new XElement("add",
                    new XAttribute("key", "globalPackagesFolder"),
                    new XAttribute("value", globalPackagesFolder))),
            new XElement("packageSources",
                new XElement("clear"),
                new XElement("add",
                    new XAttribute("key", "kairuha-local"),
                    new XAttribute("value", localSource)),
                new XElement("add",
                    new XAttribute("key", "nuget.org"),
                    new XAttribute("value", "https://api.nuget.org/v3/index.json"),
                    new XAttribute("protocolVersion", "3"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource",
                    new XAttribute("key", "kairuha-local"),
                    new XElement("package", new XAttribute("pattern", "Kairuha"))),
                new XElement("packageSource",
                    new XAttribute("key", "nuget.org"),
                    new XElement("package", new XAttribute("pattern", "*"))))));

    document.Save(path);
}

// --- 启动发布产物并检查公开契约 ---
static async Task VerifyHttp(string publish)
{
    var port = FreePort();                                  // CI 可以并行运行，不争用固定端口
    var address = new Uri($"http://127.0.0.1:{port}");
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = publish,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    start.ArgumentList.Add(Path.Combine(publish, "TemplateSmoke.dll"));
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add(address.ToString());
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // OpenAPI 只在开发环境开放

    using var server = Process.Start(start) ?? throw new InvalidOperationException("无法启动模板发布产物");
    var output = server.StandardOutput.ReadToEndAsync();     // 立即读取，避免长日志填满子进程管道
    var errors = server.StandardError.ReadToEndAsync();

    try
    {
        using (var probe = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(2) })
        {
            await WaitUntilReady(probe, server, output, errors); // 启动速度随 CI 机器变化，轮询真实端点
        }

        using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) }; // 正式契约请求允许更宽裕时间

        var greeting = await client.PostAsJsonAsync("/greetings", new { name = " 星绒 " }); // 合法请求应整理名字
        if (greeting.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"合法问候返回了 {(int)greeting.StatusCode}");
        var reply = await greeting.Content.ReadFromJsonAsync<Reply>(); // 读取模板声明的公开返回结构
        if (reply?.Message != "你好，星绒！") throw new InvalidOperationException($"问候内容不正确：{reply?.Message}");

        using var broken = new StringContent("{\"name\":", Encoding.UTF8, "application/json"); // 故意发送不完整 JSON
        var malformed = await client.PostAsync("/greetings", broken);
        if (malformed.StatusCode != HttpStatusCode.BadRequest) throw new InvalidOperationException($"损坏 JSON 返回了 {(int)malformed.StatusCode}");
        using var problem = JsonDocument.Parse(await malformed.Content.ReadAsStringAsync()); // 必须仍然是 Problem Details
        if (problem.RootElement.GetProperty("status").GetInt32() != 400) throw new InvalidOperationException("错误体里的 status 不是 400");
        if (!problem.RootElement.TryGetProperty("traceId", out _)) throw new InvalidOperationException("错误体缺少 traceId");

        var openApi = await client.GetStringAsync("/openapi/v1.json"); // 文档必须来自刚生成项目的真实路由
        using var document = JsonDocument.Parse(openApi);
        if (!document.RootElement.GetProperty("paths").TryGetProperty("/greetings", out _)) throw new InvalidOperationException("OpenAPI 缺少 /greetings");
    }
    catch (Exception exception)
    {
        if (!server.HasExited) server.Kill(entireProcessTree: true); // 先结束进程，异步日志读取才会完整收尾
        await server.WaitForExitAsync();
        throw new InvalidOperationException(
            $"模板发布产物的 HTTP 验收失败：{exception.Message}\n标准输出：\n{await output}\n错误输出：\n{await errors}",
            exception);
    }
    finally
    {
        if (!server.HasExited)
        {
            server.Kill(entireProcessTree: true);             // 只终止本次验收启动的子进程树
        }

        await server.WaitForExitAsync();                      // 等待文件和端口完全释放后再清理目录
    }
}

// --- 等待服务真正开始接收请求 ---
static async Task WaitUntilReady(
    HttpClient client,
    Process server,
    Task<string> output,
    Task<string> errors)
{
    for (var attempt = 0; attempt < 40; attempt++)           // 最多等待约二十秒，兼顾冷启动 CI
    {
        if (server.HasExited) throw new InvalidOperationException($"发布产物提前退出：\n{await output}\n{await errors}");

        try
        {
            using var response = await client.GetAsync("/openapi/v1.json");
            if (response.IsSuccessStatusCode) return;        // OpenAPI 可读意味着完整管线已经就绪
        }
        catch (HttpRequestException)
        {
            // 监听端口尚未建立，继续等待即可。
        }
        catch (TaskCanceledException)
        {
            // 冷启动超过单次两秒不代表整个验收失败。
        }

        await Task.Delay(500);
    }

    throw new TimeoutException("发布产物没有在二十秒内就绪"); // 外层 finally 会先终止进程，避免为了等日志反而卡住
}

// --- 执行一条 dotnet 命令并保留完整失败输出 ---
static async Task Dotnet(string workingDirectory, params string[] arguments)
{
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    foreach (var argument in arguments) start.ArgumentList.Add(argument); // ArgumentList 避免路径空格和引号歧义

    Console.WriteLine($"> dotnet {string.Join(' ', arguments)}");
    using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 dotnet");
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"dotnet 返回 {process.ExitCode}：\n{await output}\n{await errors}");
    }
}

// --- 检查待发布包没有夹带本机路径或私密文件 ---
static void VerifyPackages(string[] packages)
{
    var source = Path.GetFullPath(Environment.CurrentDirectory); // CI 和发布文档都要求从仓库根目录执行
    var sourceBytes = new[]
    {
        Encoding.UTF8.GetBytes(source),
        Encoding.Unicode.GetBytes(source),
    };
    var forbiddenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".db", ".env", ".key", ".pfx", ".snk",
    };

    foreach (var package in packages)
    {
        using var archive = ZipFile.OpenRead(package);       // NuGet 包本身就是 ZIP，可以逐项检查实际内容
        foreach (var entry in archive.Entries)
        {
            var parts = entry.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"包 {Path.GetFileName(package)} 夹带了构建目录：{entry.FullName}");
            }

            if (forbiddenExtensions.Contains(Path.GetExtension(entry.Name)))
            {
                throw new InvalidOperationException($"包 {Path.GetFileName(package)} 夹带了私密或数据文件：{entry.FullName}");
            }

            if (entry.Length == 0) continue;                 // 目录项和空文件没有需要扫描的字节
            using var stream = entry.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            var bytes = content.ToArray();
            if (sourceBytes.Any(value => Contains(bytes, value)))
            {
                throw new InvalidOperationException($"包 {Path.GetFileName(package)} 的 {entry.FullName} 泄露了构建机源码路径");
            }
        }
    }
}

// --- 在二进制内容里查找一段确定字节 ---
static bool Contains(byte[] content, byte[] value)
{
    if (value.Length == 0 || value.Length > content.Length) return false;

    for (var start = 0; start <= content.Length - value.Length; start++)
    {
        if (content.AsSpan(start, value.Length).SequenceEqual(value)) return true;
    }

    return false;
}

// --- 找一个短暂空闲的本地端口 ---
static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);   // 让操作系统分配，不硬编码 CI 端口
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

// --- 给需要时的 Arch 本地验证追加兼容参数 ---
static string[] CompatibilityArgument()
{
    return string.Equals(
        Environment.GetEnvironmentVariable("KAIRUHA_ALLOW_MISSING_PRUNE_PACKAGE_DATA"),
        "true",
        StringComparison.OrdinalIgnoreCase)
        ? ["-p:AllowMissingPrunePackageData=true"]
        : [];
}

// --- 把可选参数接到命令末尾 ---
static string[] Arguments(string[] required, string[] optional)
{
    return [.. required, .. optional];
}

// --- 成功后尽力清理自己的临时目录 ---
static void TryDelete(string path)
{
    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch (IOException exception)
    {
        Console.Error.WriteLine($"临时目录稍后可手动删除：{path}（{exception.Message}）");
    }
    catch (UnauthorizedAccessException exception)
    {
        Console.Error.WriteLine($"临时目录稍后可手动删除：{path}（{exception.Message}）");
    }
}

internal sealed record Reply(string Message);               // 只读取模板客户端真正看到的字段
