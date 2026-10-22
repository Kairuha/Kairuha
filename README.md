# Kairuha

[![Verify](https://github.com/Kairuha/Kairuha/actions/workflows/verify.yml/badge.svg)](https://github.com/Kairuha/Kairuha/actions/workflows/verify.yml)
[![Kairuha on NuGet](https://img.shields.io/nuget/vpre/Kairuha.svg?label=Kairuha)](https://www.nuget.org/packages/Kairuha)
[![Kairuha.Templates on NuGet](https://img.shields.io/nuget/vpre/Kairuha.Templates.svg?label=Kairuha.Templates)](https://www.nuget.org/packages/Kairuha.Templates)

Kairuha 是一个按 [HOP](https://github.com/kernel4632/HOP) 思想设计的 .NET 后端框架。

它建立在 ASP.NET Core Minimal APIs 上，只负责统一错误、边界响应和 OpenAPI。业务继续使用原生路由、依赖注入、配置、日志与数据库，不需要经过 Controller → Service → Repository 的转手链。

> 当前状态：`0.1.0-alpha.1` 已发布。核心契约、完整示例和模板验收已经在 Windows、Linux、macOS 跑通；Alpha 阶段不承诺 API 已稳定。

Kairuha 使用 [MIT License](LICENSE)。

## 快速开始

需要 .NET 10 SDK：

```bash
dotnet new install Kairuha.Templates::0.1.0-alpha.1
dotnet new kairuha -n MyApi
cd MyApi
dotnet run
```

生成的项目包含 `/greetings` 示例与开发环境 OpenAPI，可以直接替换成自己的业务主体。

## 从源码运行

需要 .NET 10 SDK 和 ASP.NET Core 10 Runtime。

```bash
dotnet restore Kairuha.slnx
dotnet build Kairuha.slnx --no-restore
dotnet test Kairuha.slnx --no-build
dotnet run --project samples/Ledger --urls http://localhost:5080
```

Arch Linux 会把 ASP.NET Core 的编译引用单独拆成包。如果 `dotnet restore` 报 `NETSDK1226`，安装对应的 Targeting Pack：

```bash
sudo pacman -S aspnet-targeting-pack
```

只做临时验证时，也可以给 `restore` 增加 `-p:AllowMissingPrunePackageData=true`；正式开发更建议补齐 Targeting Pack。

Ledger 示例提供：

```text
POST   /bills                 记一笔账
GET    /bills?month=2026-10   列出账单
DELETE /bills/{id}            删除一笔账
GET    /sum?month=2026-10     月度汇总
GET    /openapi/v1.json       开发环境 OpenAPI
```

完整调用方法和代码导航见 [Ledger 示例说明](samples/Ledger/README.md)。

## 验证项目模板

发布前验收使用真正的 NuGet 包，而不是项目引用：

```bash
mkdir -p artifacts/packages
dotnet pack src/Kairuha/Kairuha.csproj -c Release -o artifacts/packages
dotnet pack templates/Kairuha.Templates/Kairuha.Templates.csproj -c Release -o artifacts/packages
dotnet run --project tests/Kairuha.TemplateSmoke -c Release -- artifacts/packages
```

验收程序会在隔离临时目录完成 `install → new → restore → build → publish → HTTP`，并检查合法请求、非法 JSON 和 OpenAPI。GitHub Actions 会在 Linux、Windows、macOS 上运行同一条路径。

## 应用写法

```csharp
using Kairuha;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddKairuha();
builder.Services.AddValidation();

var app = builder.Build();
app.UseKairuha();

app.MapPost("/bills", Bill.Add);
app.MapGet("/bills", Bill.List);

app.Run();
```

`AddValidation()` 必须留在应用项目中。它使用 .NET 10 的源码生成器发现当前程序集里的 Minimal API 输入类型；如果把调用藏进 Kairuha 程序集，应用自己的类型不会被发现。

## 错误

主体指令可以抛出用户能够自行修正的错误：

```csharp
throw Error.Input("金额必须大于 0");    // 400
throw Error.Missing("没有找到这笔账");  // 404
throw Error.Conflict("账单已经存在");   // 409
```

Kairuha 使用标准 Problem Details 返回错误。生产环境的未知异常只返回 `traceId`，完整原因进入服务日志；开发环境会同时返回具体原因。

## 项目结构

```text
src/Kairuha/          最小运行包
samples/Ledger/       可以直接运行的完整记账服务
templates/            dotnet new kairuha 项目模板
tests/Kairuha.Tests/  HTTP 集成测试
tests/Kairuha.TemplateSmoke/  从包开始的项目模板验收
.github/workflows/    三平台持续集成
PRD.md                产品范围、取舍和验收条件
```

设计约束和后续范围见 [PRD.md](PRD.md)，版本变化见 [CHANGELOG.md](CHANGELOG.md)，正式发布步骤见 [RELEASING.md](RELEASING.md)。
