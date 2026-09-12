# 贡献指南

开发需要 .NET 10 SDK 和 .NET 8 runtime，具体 SDK 由 global.json 约束。

1. 阅读 [能力矩阵](docs/CAPABILITIES.md) 和 [迁移说明](docs/MIGRATION.md)。
2. 按 [测试环境](docs/ENVIRONMENT_BOOTSTRAP.md) 构建并运行单元/集成测试。
3. 公共 API 使用领域命名空间和 XML 文档，Task/ValueTask 方法使用 Async 后缀；JSON 字段遵循 Nacos 3.2.4 契约。
4. 包版本在 Directory.Packages.props 中集中定义。不要提交 bin/obj、project.assets.json、日志、TRX 或真实凭据。
5. 行为修改和全量格式化分开提交；新增服务器能力必须有契约测试和实际联调证据。没有服务端能力时应明确拒绝，不能返回伪成功。
6. 常规 CI 在本地临时 Nacos 容器中测试。云端写入测试只能操作本次创建的随机资源，不能清空公共命名空间或重启共享服务器。

本地检查：

```text
dotnet build RedNb.Nacos.sln -c Release
dotnet test RedNb.Nacos.sln --no-build -c Release -m:1 -p:TestTfmsInParallel=false
dotnet format whitespace RedNb.Nacos.sln --no-restore --verify-no-changes
git diff --check
```

历史 archive 文档不是当前开发指令。项目维护任务遵循用户指定的分支规则；外部贡献者可通过 Fork 提交 PR。
