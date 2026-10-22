# Kairuha 产品需求文档

> 状态：0.1 Alpha 发布候选（R1）
> 日期：2026-10-22
> 技术基线：.NET 10 LTS / ASP.NET Core Minimal APIs
> 设计依据：HOP `86601b3`

## 一句话说明

Kairuha 是一个按 HOP 思想设计的 .NET 后端框架。

它不重新发明 HTTP、依赖注入、日志、JSON 和数据库，而是在 ASP.NET Core 之上给出一套轻、直、能长期维护的默认写法：入口只接请求，主体文件写完整业务，数据只有一个来源，错误只在入口统一处理。

开发者打开项目后，不需要先理解 Controller、Service、Repository、Handler、Manager、Mediator 等技术分层，就能沿着一条线找到真正的业务代码。

## 为什么要做

ASP.NET Core 已经可以很快写出一个接口，但项目被 AI 或脚手架继续扩展后，往往会变成下面这样：

```text
Controllers/BillController.cs
Services/BillService.cs
Repositories/BillRepository.cs
Models/BillModel.cs
Dtos/CreateBillRequest.cs
Validators/CreateBillValidator.cs
Mappers/BillMapper.cs
```

“记一笔账”本来只有几步，最后却要跨过许多只负责转手的文件。接手者不知道应该改哪里，也不知道改一处会影响什么。

Kairuha 要解决的不是“如何再封装一层 ASP.NET Core”，而是四个更直接的问题：

1. 怎样让人从接口一眼找到业务。
2. 怎样让一个主体的全部规则待在同一个地方。
3. 怎样把格式解析、统一错误和 OpenAPI 等非业务工作交给框架。
4. 怎样让 AI 继续开发时，不会自然长出越来越多的层和样板代码。

## 产品判断

Kairuha 是一层有明确取舍的约定，不是新的 Web 运行时。

- 底层继续使用 Kestrel、ASP.NET Core 路由、绑定、依赖注入、日志和配置。
- 默认使用 Minimal APIs，不建立自己的 Controller 体系。
- 不把每个接口包装成一个类，也不要求 Request、Response、Validator 各占一个文件。
- 不引入中介者、消息总线或 Repository 作为默认路径。
- 用户随时可以直接使用 ASP.NET Core API，不需要等待 Kairuha 再封装一遍。
- Kairuha 自己没有价值的代码就不写；能稳定交给 .NET 的能力直接交给 .NET。

## 面向谁

首要用户：

- 用 AI 辅助开发后端的个人开发者和小团队。
- 希望项目半年后仍能快速接手的人。
- 需要构建 API、内部服务、小游戏后端或轻量 SaaS 的 .NET 开发者。
- 觉得传统分层架构对当前项目过重，但又不想把所有代码塞进 `Program.cs` 的开发者。

暂时不是首要用户：

- 必须遵循既有企业分层规范的大型组织。
- 需要完整事件溯源、工作流编排或分布式消息平台的系统。
- 主要需求是 MVC 页面、Razor Pages 或完整前后端一体方案的项目。

## HOP 如何落到 Kairuha

| HOP 原则 | Kairuha 的产品决定 | 判断是否做到 |
|---|---|---|
| 按主体组织 | `Bill.cs` 放账单的增加、查询、删除和汇总，不拆成技术层 | 只看文件名就能猜到业务在哪 |
| 加删只动一处 | 业务规则只改主体文件；只有确实持续增长的同类扩展才使用自动发现 | 不为几条普通路由制造扫描器 |
| 数据一个来源 | 输入结构同时供绑定、验证和 OpenAPI 使用；同一份数据不再做 DTO 到 Model 的无意义映射 | 改字段时不需要同步多个类型 |
| 流程一条线 | 请求 → 入口路由 → 主体指令 → 数据 → 返回 | 一般两三个文件能追完一次请求 |
| 打开就懂 | 模板要求文件头说明职责、调用方式和可复制示例 | 第一次打开文件就知道怎么使用 |
| 装得下 | 一个方法只保留少量同时活动的概念，过长时按真实业务步骤拆 | 不用记住一串框架对象才能读懂 |
| 平铺直叙 | 直接调用，不用继承链、事件总线或隐藏分发 | 调用关系能靠代码顺序看出来 |
| 信任内部数据 | 格式错误由入口处理，业务输入在主体指令开头检查一次，内部继续调用时不重复检查 | 业务代码里没有层层防御 |
| 说人话 | 使用 `Bill.Add()`、`Bill.List()`，不用 `BillService.CreateBillAsync()` | 名字描述业务，不描述架构角色 |
| 注释讲业务 | 注释说明每一步做什么和为什么这么选，不复述语法 | 只扫注释也能看懂主要流程 |
| 修问题做减法 | 修复优先消除产生问题的路径，不新增永久补丁层 | 普通修复后代码量持平或减少 |
| 改到哪里整理到哪里 | 修改过的区域同时补清楚名字、注释和边界 | 项目不会一边开发一边继续腐化 |

## 核心体验

### 创建项目

目标命令：

```bash
dotnet new install Kairuha.Templates
dotnet new kairuha -n Ledger
cd Ledger
dotnet run
```

生成的项目默认只有真正需要的文件：

```text
Ledger/
├── Ledger.csproj   项目依赖和 .NET 版本
├── Program.cs      入口：路由、统一错误和启动
├── Greeting.cs     问候：输入、规则和返回
└── README.md       怎么运行、怎么调用、从哪里继续改
```

模板不为尚不存在的数据访问创建空文件；业务真正需要持久化时，再增加唯一的 `Data.cs`。主体增加到一眼扫不过来时，再按业务归类；模板不提前创建空的 `Controllers/`、`Services/`、`Repositories/`、`Models/` 等目录。

### 写入口

目标代码形态：

```csharp
using Kairuha;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddKairuha();
builder.Services.AddValidation();

var app = builder.Build();
app.UseKairuha();

app.MapPost("/bills", Bill.Add);
app.MapGet("/bills", Bill.List);
app.MapDelete("/bills/{id}", Bill.Remove);
app.MapGet("/sum", Bill.Sum);

app.Run();

public partial class Program;
```

路由保持显式，因为几条路由直接写出来比扫描、特性或约定注册更容易读。入口只说明“什么请求交给什么指令”，不写业务判断。

`AddValidation()` 留在应用程序集，因为 .NET 10 的验证源码生成器需要从调用位置发现该应用的输入类型；Kairuha 不把这个调用藏进框架程序集。

### 写主体

主体文件的目标形态：

```csharp
/*
账单：账单的全部操作都在这里。
入口把请求直接交给 Add、List、Remove 和 Sum。
调用示例：Bill.List("2026-10", data)
*/
public static class Bill
{
    public sealed record New(decimal Amount, string Category, string Note, DateOnly Date);
    public sealed record Item(Guid Id, decimal Amount, string Category, string Note, DateOnly Date);

    // --- 记一笔账 ---
    public static async Task<Item> Add(New input, Data data)
    {
        var category = input.Category.Trim();                     // 去掉用户不小心带上的首尾空格
        if (input.Amount <= 0) throw Error.Input("金额必须大于 0"); // 用户输入只在业务入口检查一次
        if (category.Length == 0) throw Error.Input("必须填写分类"); // 没有分类就无法做后续汇总

        var bill = new Item(Guid.NewGuid(), input.Amount, category, input.Note, input.Date); // 组成一笔完整账单
        data.Bills.Add(bill);                                      // 把账单写进唯一的数据来源
        await data.SaveChangesAsync();                             // 确认这次记账已经保存
        return bill;                                               // 返回刚刚保存的账单
    }
}
```

这是代码形态说明，不是最终 API 定稿。原型阶段必须验证 Minimal API 绑定、OpenAPI、AOT 和测试工具是否都能保留这种直接写法。

## 请求路径

```text
HTTP 请求
   ↓
Program.cs：匹配路由、解析外部格式
   ↓
Bill.cs：检查业务输入、执行完整业务
   ↓
Data.cs 或 Bill 自己的数据：唯一读写位置
   ↓
ASP.NET Core：序列化并返回结果

任意位置抛错
   └── Kairuha 入口统一区分可修改错误与程序故障
```

这条路径中不允许出现只负责把调用继续转交下去的层。

## MVP 范围

### P0：必须交付

#### 1. 项目模板

- 提供 `dotnet new kairuha`。
- 默认目标框架为 `net10.0`。
- 默认开启 Nullable 和 Implicit Usings。
- 默认使用 `CreateSlimBuilder`。
- 生成一个可以立即运行、立即请求的完整小项目。
- README 明确告诉用户入口、主体、数据分别在哪里。

#### 2. 最小运行包

NuGet 包名为 `Kairuha`，首个公开版本为 `0.1.0`。

运行包只负责下面几件事：

- 注册统一错误处理。
- 注册边界参数验证所需的 .NET 标准能力。
- 注册 OpenAPI 文档生成。
- 提供少量业务错误构造方法。
- 保留对原生 `WebApplication`、服务容器、配置和日志的直接访问。

不为已有的 ASP.NET Core API 再创建一套同名包装。

#### 3. 两类错误

业务里允许直接抛出：

```csharp
throw Error.Input("金额必须大于 0");   // 400
throw Error.Missing("没有找到这笔账"); // 404
throw Error.Conflict("这笔账已经存在"); // 409
```

框架统一返回 RFC Problem Details：

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "请求内容不正确",
  "status": 400,
  "detail": "金额必须大于 0",
  "traceId": "00-..."
}
```

规则：

- 用户可以自行修正的错误，返回对应状态码和可读原因。
- JSON 语法、类型绑定等外部格式错误视为用户输入错误。
- 未标记异常视为程序故障，记录完整异常并返回 500。
- 开发环境可以返回具体故障原因。
- 生产环境不把堆栈、SQL、路径或密钥等内部信息发给客户端，只返回固定说明和 `traceId`；完整原因留在服务日志中。这里为了安全，对 HOP 的“带上原因”采用“客户端拿定位编号、服务端保留原始原因”的实现。

#### 4. 边界检查

- 请求 JSON、路由参数、查询参数和表单继续使用 ASP.NET Core 的标准绑定。
- 结构层约束可以使用 .NET 标准验证能力，并反映到 OpenAPI。
- 业务约束写在被路由直接调用的主体方法开头。
- 不生成独立的 `Validators/` 文件夹。
- 不在内部函数重复进行类型、空值和格式检查。
- 输入需要先整理再检查，例如先 `Trim()`，再判断是否为空。

#### 5. 单一来源的接口描述

- 一个 C# record/class 同时作为绑定、验证、JSON 和 OpenAPI 的来源。
- 默认不要求 Request DTO、Domain Model 和 Response DTO 三份相同结构。
- 只有外部契约与内部数据确实不同时才允许单独建类型。
- OpenAPI 文档由实际路由生成，不维护第二份手写接口表。

#### 6. 与 ASP.NET Core 共存

以下原生能力必须可以直接使用：

- `MapGroup` 与路由元数据。
- ASP.NET Core Authorization。
- CORS、限流和标准中间件。
- 任意 DI 注册。
- EF Core、Dapper 或直接文件存储。
- `IResult`、TypedResults、流式响应和文件响应。

Kairuha 不得要求用户把现有生态适配成自己的接口。

#### 7. 可测试

- 模板保留 `public partial class Program`，可以直接使用 `WebApplicationFactory` 做进程内 HTTP 测试。
- 业务主体使用普通静态方法或普通对象，不依赖只能由框架创建的基类。
- 错误响应、非法 JSON、参数绑定、OpenAPI 和至少一个完整业务路径必须有集成测试。
- 发布前必须从模板创建一个全新项目并跑通 `restore → build → test → publish`。

#### 8. 文档和完整示例

必须提供一个 Ledger 示例，覆盖：

- 记账、查询、删除和月度汇总。
- 用户输入错误与找不到数据。
- 一份数据只由一个主体负责读写。
- 金额按最小单位计算，避免浮点误差。
- OpenAPI 与 HTTP 集成测试。
- 从入口追到数据不超过三个文件。

文档先教用户完成真实功能，再解释框架概念；不先展示一屏配置项。

### P1：验证 MVP 后再做

- Native AOT 发布模板和 CI 验证。
- 数据库、认证、上传、SSE 和 WebSocket 的 HOP 写法示例。
- 当某类扩展确实会持续增加时，提供可预测、AOT 兼容的编译期发现机制。
- 可选的项目检查工具，只报告确定的问题，不给项目打抽象的“HOP 分数”。
- 文档站和 Kairuha 框架娘视觉形象。

### 不在 MVP 内

- 自研 HTTP 服务器、依赖注入容器、日志系统或 JSON 序列化器。
- 自研 ORM、Repository 或 Unit of Work。
- CQRS、Mediator、事件总线和工作流引擎。
- 自动扫描所有路由。
- 强制一种数据库或部署平台。
- 自动生成大量业务文件的代码生成器。
- 图形化后台、前端组件库或低代码平台。
- 用静态规则机械禁止所有大文件、静态方法或特定目录名。

## 默认行为

| 情况 | 默认结果 |
|---|---|
| 合法对象返回 | `200` 和 JSON |
| 显式返回 `Created` | `201` 和 Location |
| 没有内容 | `204` |
| JSON 写坏或类型不匹配 | `400` 和可读错误 |
| `Error.Input` | `400` |
| `Error.Missing` | `404` |
| `Error.Conflict` | `409` |
| 未处理异常，开发环境 | `500`、错误原因和 traceId |
| 未处理异常，生产环境 | `500`、固定说明和 traceId；完整异常写日志 |
| OpenAPI | 开发环境默认提供，生产环境由用户显式开启 |
| CORS | 默认关闭，不使用任意来源通配 |
| 认证 | 默认不假装安全；需要保护的路由必须显式声明 |

## 非功能要求

### 可理解性

- 新接手者只看目录，应能指出入口和每个业务主体。
- 给定一条路由，应能在 60 秒内找到实际业务方法。
- 一次普通请求的主要流程不得经过超过三个项目文件。
- 修改已有业务规则通常只改一个主体文件。
- 新增一条普通接口最多改入口和对应主体两个位置。

### 性能

- Kairuha 不在每次请求上增加反射扫描或动态分发。
- 相比等价 Minimal API，框架本身的请求开销目标是不超过基准噪声；发布前以 BenchmarkDotNet 和端到端压测给出实测结果，不能先写未经验证的性能数字。
- 启动期工作必须有限、可观察，不能扫描整个磁盘或加载无关程序集。

### 兼容性

- 首版只支持当前 LTS：.NET 10。
- 每个受支持的 .NET 补丁版本都通过模板集成测试。
- Kairuha 公共 API 在 `1.0` 后遵循语义化版本。
- `0.x` 允许调整，但每次破坏性变化必须写迁移说明。

### 安全

- 生产环境不泄露内部异常细节。
- 不默认开启宽松 CORS。
- 不内置示例密钥或固定口令。
- 所有安全能力尽量复用 ASP.NET Core 官方实现。
- 框架的错误处理不得吞掉日志、取消信号或进程应当终止的严重异常。

### 可观察性

- 每个错误响应包含 `traceId`。
- 使用 `Microsoft.Extensions.Logging`，不再包装一套日志门面。
- 日志内容包括路由、状态码、耗时和未处理异常，但默认不记录请求密钥、令牌和完整敏感正文。

## 验收场景

### 场景一：第一次使用

1. 用户安装模板并创建项目。
2. 不修改代码直接运行成功。
3. 按 README 发出一个合法请求，得到 200。
4. 发出非法 JSON，得到 400 和能看懂的原因。
5. 打开 OpenAPI，内容与实际接口一致。

### 场景二：增加业务

1. 用户增加“账单备注不能为空”的规则。
2. 只修改 `Bill.cs` 中接收请求的指令。
3. 合法请求行为不变，非法请求得到 400。
4. 不需要新增 Validator、Service 或 Mapper。

### 场景三：定位故障

1. 主体方法抛出一个未标记异常。
2. 客户端得到 500 和 `traceId`。
3. 日志能用同一个 `traceId` 找到完整异常。
4. 生产响应中看不到堆栈、数据库语句或本地路径。

### 场景四：离开框架快捷方式

1. 用户为一组路由调用原生 `MapGroup()`。
2. 用户直接添加 ASP.NET Core 的认证、限流或第三方中间件。
3. Kairuha 的错误约定、验证和 OpenAPI 仍然有效。
4. 用户不需要实现 Kairuha 专用适配器。

### 场景五：Native AOT（P1）

1. 示例项目执行 `dotnet publish -c Release -r <RID> -p:PublishAot=true`。
2. 构建不产生 Kairuha 自身导致的 AOT 或裁剪警告。
3. 发布产物启动后，业务接口、错误处理和 OpenAPI 按声明范围工作。

## 成功指标

首个公开版本满足以下条件才算完成：

- 模板项目可在 Windows、Linux、macOS 上构建和运行。
- 从创建项目到第一次成功请求，文档路径不超过五分钟。
- Ledger 示例的任一业务流程不超过三个项目文件。
- 运行时主包没有非必要第三方依赖。
- 参考项目里不存在 Controller → Service → Repository 式转手链。
- 至少用三个不同的小型真实项目试用，再冻结 `1.0` API。
- 试用者能在不询问作者的情况下完成一次增、一次改、一次删。

## 发布阶段

### R0：契约原型

- [x] 验证 `AddKairuha()` / `UseKairuha()` 的最小 API。
- [x] 跑通绑定、验证、错误、OpenAPI、测试和普通发布。
- [x] 确认没有为了简短 API 破坏 ASP.NET Core 原生能力。

### R1：0.1 Alpha

- [ ] 发布 `Kairuha` 和 `Kairuha.Templates`；包已可生成，等待许可证、仓库地址、NuGet 所有权与密钥确认。
- [x] 完成 Ledger 示例与入门文档。
- [x] 配置三平台 CI，从空目录安装模板并完成还原、构建、发布与 HTTP 验证；工作流需在正式 GitHub 仓库首次运行后确认各托管环境结果。

### R2：0.5 Beta

- 用三个真实项目验证结构。
- 根据真实修改任务调整 API，而不是根据想象继续加功能。
- 补齐认证、数据库、上传和流式返回的写法说明。
- 完成 Native AOT 验证。

### R3：1.0

- 冻结核心入口、错误结构和模板约定。
- 发布升级政策、兼容性政策和安全说明。
- 删除试用中没有实际价值的 API，再进入稳定版本。

## 主要风险

### 太薄，用户认为没有必要

这是最真实的风险。Kairuha 如果只把两行 ASP.NET Core 配置换个名字，就不应该存在。

应对方式：价值必须来自一整套可以直接使用的 HOP 默认结构、统一错误契约、模板、示例和长期维护体验，而不是 API 缩写。每个新增封装都要证明它减少了项目里的重复工作。

### 太魔法，反而违反 HOP

自动扫描路由、按方法名猜 HTTP 动词、运行时拼接调用关系，短期看起来更少，长期会让接手者找不到真实路径。

应对方式：MVP 路由显式登记；自动发现只用于“会不断增加”的同类扩展，并且规则必须在代码和构建产物中都可见。

### 把 HOP 变成死板检查表

HOP 的数字是参考刻度，不是考试线。机械限制文件行数或禁止某个词，会逼出更难懂的代码。

应对方式：框架提供默认路径和真实示例，不对主观架构风格做强制编译错误。

### 为隐藏框架细节而泄露安全信息

把所有 500 原因直接返回客户端虽然方便调试，但可能泄露数据库、路径和密钥。

应对方式：开发环境显示原因；生产环境返回 traceId，原始原因写入同一 trace 下的日志。

### 与 .NET 版本绑得过紧

Kairuha 依赖 Minimal APIs、验证和 OpenAPI 的官方行为，.NET 升级可能改变细节。

应对方式：首版只支持 .NET 10 LTS；每月补丁通过生成项目集成测试，不同时兼容一长串即将停止支持的版本。

## 已确定的决定

- 名称：Kairuha。
- 首版语言与平台：C# / .NET 10。
- Web 基础：ASP.NET Core Minimal APIs。
- 默认组织方式：入口 + 按主体命名的业务文件。
- 路由：显式登记，不默认自动扫描。
- 数据：不绑定 ORM，示例优先使用 EF Core SQLite。
- 错误：业务错误显式抛出，程序错误入口统一记录。
- OpenAPI：从实际路由生成。
- 错误体：采用 RFC Problem Details，验证错误保留标准 `errors` 字段。
- 业务错误：`Error` 是可直接抛出的异常类型，由唯一入口转换为响应。
- 初始化：保留 `AddKairuha()` 与 `UseKairuha()`，让服务注册和中间件顺序保持可见。
- 验证：应用显式调用 `AddValidation()`，不跨程序集隐藏源码生成入口。
- 示例数据库：Ledger 使用 EF Core SQLite，但运行包不依赖 EF Core。
- 许可证：MIT。

## 契约原型验证结果

- 解决方案在 .NET 10 上零警告构建，HTTP 集成测试覆盖完整业务路径、验证、非法 JSON、404、生产 500 和 OpenAPI。
- `Kairuha` 与 `Kairuha.Templates` 均可打包；模板已在隔离环境完成安装、创建、还原、构建和真实 HTTP 请求。
- Minimal API 绑定、.NET 10 验证与 OpenAPI 可以保留显式路由和直接主体调用。
- 普通 .NET 发布已经完成验证；Native AOT 仍留在 P1。

## 仍需验证

- RFC Problem Details 对不同前端客户端生成器的兼容性；原型已验证 HTTP 契约，但还没有覆盖多种生成器。
- .NET 10 验证与 OpenAPI 在 Native AOT 下的覆盖范围。
- EF Core SQLite 作为入门示例的学习成本，需要通过真实试用者反馈确认。

## 参考

- [HOP：Human-Oriented Programming](https://github.com/kernel4632/HOP)
- [.NET 官方支持策略](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [ASP.NET Core 路由与 Route Groups](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [ASP.NET Core Native AOT](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot)
