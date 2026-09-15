# 2.1.0 发布记录

发布日期：2026-09-15。GitHub Release 和六个 NuGet 包均已发布并验证可公开下载。

| 项目 | 记录 |
|---|---|
| 标签 | `v2.1.0` |
| 发布提交 | `2b09b451d9c849674ebba26198a2bfd9278305c2` |
| 构建验证 | [CI 34952946643](https://github.com/yinghongzhen/RedNb.Nacos/actions/runs/34952946643)，11 项检查全部成功 |
| 包来源 | 上述 CI 的 `validation-results`，artifact ID `10389897574` |
| 构建压缩包 SHA-256 | `2be75458000aacd22797c63c530e50494c076713a9b316b8418ffc4ffc18cf12` |
| GitHub Release | [v2.1.0](https://github.com/yinghongzhen/RedNb.Nacos/releases/tag/v2.1.0)，正式版、Latest、六个 nupkg 附件 |
| NuGet 发布 | [可信发布工作流 34953935898](https://github.com/yinghongzhen/RedNb.Nacos/actions/runs/34953935898)，成功 |

六个包：RedNb.Nacos、RedNb.Nacos.Http、RedNb.Nacos.Grpc、RedNb.Nacos.DependencyInjection、RedNb.Nacos.AspNetCore、RedNb.Nacos.All，版本均为 2.1.0。

验证内容：

- 六包身份、版本、双 TFM、README、许可证及 SDK 内部依赖版本校验通过。
- 六包的 repository commit 均与发布提交一致。
- GitHub 上传附件 SHA-256 与 CI 包逐一匹配。
- NuGet 可信发布工作流逐一返回推送成功。
- 从 NuGet 公共下载地址重新下载六个包，除 NuGet 仓库签名文件外，包内文件集合与字节内容均与 Release 附件一致。
- 发布完成后更新仓库中英文 README 的稳定版标识与安装命令。标签及已发布附件保持不变。

完整功能验收与未认证范围见 [NativeAOT 测试报告](NATIVEAOT_TEST_REPORT.md)。本次发布未覆盖旧的 2.0.0 版本。
