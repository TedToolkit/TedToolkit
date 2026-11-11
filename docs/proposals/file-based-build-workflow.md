# 用 `build.cs` 取代 `Build.csproj`

TedToolkit 已通过 `.github/workflows/build.yml` 的 `workflow_call` 向其他仓库提供共享工作流。该工作流在调用仓库中执行 `dotnet run --project Build/Build.csproj`，因此每个调用仓库仍要维护自己的构建项目。

## 提议

- 在 TedToolkit 集中维护 .NET 10 单文件构建入口 `build.cs`，通过 `dotnet run --file <脚本路径>/build.cs` 执行。迁移后移除 TedToolkit 的 `Build/Build.csproj` 和对应的解决方案条目。
- 沿用 `.github/workflows/build.yml`。工作流检出调用仓库，显式获取与工作流版本配套的 TedToolkit 脚本，并从调用仓库根目录运行。下游仓库只需用 `jobs.<job_id>.uses` 引用共享工作流并配置所需变量。
- 脚本通过进程环境变量接收解决方案路径、测试目标等配置。共享工作流将调用仓库的 GitHub Actions `vars` 和所需 `secrets` 映射到运行步骤的 `env`。例如，可将 `vars.BUILD_SOLUTION` 映射为 `BUILD_SOLUTION`。必填变量缺失时，脚本应在执行构建前指出缺少的变量名。

## 验收

- TedToolkit 自身通过 `build.cs` 完成现有构建检查并上传产物。
- 使用 Results 验证跨仓调用。删除验证分支上的 `Build/Build.csproj` 后，仅通过共享工作流和环境变量运行，确认构建和测试针对 Results，产物也来自 Results。
- 在替换现有共享入口前，为仍引用 `build.yml@development` 的调用仓库验证迁移路径并配齐所需变量。

[GitHub Actions 可复用工作流](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows) · [.NET 单文件应用](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
