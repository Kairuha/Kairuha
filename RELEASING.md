# Kairuha 发布清单

这份清单用于发布 `Kairuha` 和 `Kairuha.Templates`。发布 NuGet 是不可原地覆盖的外部动作，必须人工确认后执行。

## 发布前决定

- [x] 使用 MIT License，并在两个包中写入 `PackageLicenseExpression` 与许可证正文。
- [x] 包元数据使用正式地址 `https://github.com/Kairuha/Kairuha`。
- [x] 创建 `Kairuha` 组织和公开的 `Kairuha/Kairuha` 仓库，并将 `main` 作为默认分支。
- [x] 在 Windows、Linux、macOS 托管环境跑通构建、测试、打包和模板端到端验收。
- 在 NuGet 账户中确认 `Kairuha` 与 `Kairuha.Templates` 的所有权；公开查询截至 2026-10-22 没有发现这两个包，但首次推送结果才是最终判断。
- 创建只允许推送这两个包 ID 的短期 NuGet API Key，不把密钥写进文件、命令历史或 CI 日志。

## 更新版本

包版本只有一份构建来源；发布时修改：

```text
Directory.Build.props 中的 KairuhaVersion
templates/Kairuha.Templates/content/Kairuha.Web.csproj
```

模板打包目标会检查这两个值，不一致时直接失败。随后更新 [CHANGELOG.md](CHANGELOG.md)，并确认模板引用的是即将发布的运行包版本。

## 本地验收

```bash
dotnet restore Kairuha.slnx
dotnet build Kairuha.slnx -c Release --no-restore
dotnet test Kairuha.slnx -c Release --no-build --no-restore
dotnet format Kairuha.slnx --verify-no-changes --no-restore

mkdir -p artifacts/packages
dotnet pack src/Kairuha/Kairuha.csproj -c Release --no-build --no-restore -o artifacts/packages
dotnet pack templates/Kairuha.Templates/Kairuha.Templates.csproj -c Release --no-build --no-restore -o artifacts/packages
dotnet run --project tests/Kairuha.TemplateSmoke -c Release --no-build --no-restore -- artifacts/packages
```

确认两个包都没有警告，并检查包内容里没有本地数据库、密钥、构建缓存或源码绝对路径。

仓库统一把候选包放在 `artifacts/packages/`；根目录不会生成 `bin/Release/`。

## 推送顺序

先发布运行包，确认 NuGet 已能解析它，再发布引用该版本的模板包：

```bash
dotnet nuget push artifacts/packages/Kairuha.0.1.0-alpha.1.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key "$NUGET_API_KEY" \
  --skip-duplicate

dotnet nuget push artifacts/packages/Kairuha.Templates.0.1.0-alpha.1.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key "$NUGET_API_KEY" \
  --skip-duplicate
```

发布后从一个与源码仓库无关的空目录执行：

```bash
dotnet new install Kairuha.Templates::0.1.0-alpha.1
dotnet new kairuha -n FirstKairuha
dotnet run --project FirstKairuha
```

最后检查 `/greetings`、非法 JSON、`/openapi/v1.json` 和 NuGet 包页面。发现问题时发布新版本，不删除并覆盖已经被使用的版本。
