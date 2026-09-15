# 构建与测试环境

需要 .NET 10 SDK（global.json 允许同主版本较新 feature band）、.NET 8 runtime、Docker，以及 Nacos 3.2.4。

```powershell
dotnet restore RedNb.Nacos.sln
dotnet build RedNb.Nacos.sln -c Release
dotnet test RedNb.Nacos.sln --no-build -c Release -m:1 -p:TestTfmsInParallel=false --logger trx --results-directory artifacts/test-results
python scripts/verify-test-results.py artifacts/test-results --minimum-runs 9
dotnet pack RedNb.Nacos.sln --no-build -c Release -o artifacts/packages
python scripts/verify-packages.py artifacts/packages --version 2.1.0
```

测试前使用新的结果目录，避免旧 TRX 混入统计。按项目和目标框架顺序执行，避免多个 WireMock 测试宿主争用本机资源。

## 集成测试变量

| 环境变量 | 含义 | 本地默认 |
| --- | --- | --- |
| NACOS_TEST_SERVER | SDK API 地址（host:port） | localhost:8848 |
| NACOS_TEST_CONSOLE | 控制台地址 | localhost:8080 |
| NACOS_TEST_USERNAME | 用户名 | nacos |
| NACOS_TEST_PASSWORD | 密码 | nacos，仅本地测试 |
| NACOS_TEST_NAMESPACE | Config/Naming 测试空间 | public/空 tenant |
| NACOS_TEST_FAULT_CONTAINER | 启用重启测试的本地容器名 | 不设置则跳过故障注入 |

远程联调先创建独立 namespace，再把连接信息放进当前进程环境变量。不要将真实凭据写入仓库或控制台命令记录文件。测试资源使用随机名称，namespace 生命周期测试仅删除自身创建的空间。MCP/A2A Console 限定 public 时使用 audit/it 前缀随机资源并逐项清理。

故障注入只允许 localhost/127.0.0.1，容器名必须匹配 rednb-nacos-*。CI 使用临时容器；云端不进行重启、清库或网络中断。

普通 CI 不依赖云端凭据。GitHub workflow 已覆盖 master 的 push/PR，并校验零测试发现、失败测试和实际 NuGet 包内容。
