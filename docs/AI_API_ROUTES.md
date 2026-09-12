# AI 方法路由清单

由当前 facade 的公开方法整理。实现路由不等于所有参数组合都已端到端验证；主要行为证据见 CAPABILITIES.md 与 TEST_REPORT.md。

| 方法 | 重载数 | 路由/状态 |
| --- | --- | --- |
| CreateAgentSpecDraftAsync | 1 | HTTP Client/Admin |
| CreatePromptDraftAsync | 1 | HTTP Client/Admin |
| CreateSkillDraftAsync | 1 | HTTP Client/Admin |
| DeleteAgentAsync | 1 | HTTP Console（public） |
| DeleteAgentSpecAsync | 1 | HTTP Client/Admin |
| DeleteAgentSpecDraftAsync | 1 | HTTP Client/Admin |
| DeleteMcpServerAsync | 1 | HTTP Console（public） |
| DeleteMcpToolAsync | 1 | 不支持独立 tool 操作 |
| DeletePromptAsync | 1 | HTTP Client/Admin |
| DeletePromptDraftAsync | 1 | HTTP Client/Admin |
| DeleteSkillAsync | 1 | HTTP Client/Admin |
| DeleteSkillDraftAsync | 1 | HTTP Client/Admin |
| DeregisterAgentEndpointAsync | 2 | gRPC |
| DeregisterMcpServerEndpointAsync | 1 | gRPC |
| DownloadSkillZipAsync | 1 | HTTP Client/Admin |
| DownloadSkillZipByLabelAsync | 1 | HTTP Client/Admin |
| DownloadSkillZipByVersionAsync | 1 | HTTP Client/Admin |
| ForcePublishAgentSpecAsync | 1 | HTTP Client/Admin |
| ForcePublishPromptAsync | 1 | HTTP Client/Admin |
| ForcePublishSkillAsync | 1 | HTTP Client/Admin |
| GetAgentCardAsync | 3 | HTTP Console（public） |
| GetAgentSpecAsync | 2 | HTTP Client/Admin |
| GetAgentSpecByLabelAsync | 1 | HTTP Client/Admin |
| GetAgentSpecDetailAsync | 1 | HTTP Client/Admin |
| GetAgentSpecMetaAsync | 1 | HTTP Client/Admin |
| GetMcpServerAsync | 2 | HTTP Console（public） |
| GetMcpToolAsync | 1 | 不支持独立 tool 操作 |
| GetPromptAsync | 2 | HTTP Client/Admin |
| GetPromptByLabelAsync | 1 | HTTP Client/Admin |
| GetPromptMetaAsync | 1 | HTTP Client/Admin |
| GetPromptVersionDetailAsync | 1 | HTTP Client/Admin |
| GetSkillDetailAsync | 1 | HTTP Client/Admin |
| GetSkillMetaAsync | 1 | HTTP Client/Admin |
| ImportMcpServersAsync | 1 | HTTP Console（public） |
| ListAgentCardsAsync | 1 | HTTP Console（public） |
| ListAgentSpecsAsync | 1 | HTTP Client/Admin |
| ListAgentVersionInfosAsync | 1 | HTTP Console（public） |
| ListAgentVersionsAsync | 1 | HTTP Console（public） |
| ListMcpServersAsync | 1 | HTTP Console（public） |
| ListPromptVersionsAsync | 1 | HTTP Client/Admin |
| ListPromptsAsync | 1 | HTTP Client/Admin |
| ListSkillsAsync | 1 | HTTP Client/Admin |
| OfflineAgentSpecAsync | 1 | HTTP Client/Admin |
| OfflinePromptAsync | 1 | HTTP Client/Admin |
| OfflineSkillAsync | 1 | HTTP Client/Admin |
| OnlineAgentSpecAsync | 1 | HTTP Client/Admin |
| OnlinePromptAsync | 1 | HTTP Client/Admin |
| OnlineSkillAsync | 1 | HTTP Client/Admin |
| PublishAgentSpecAsync | 1 | HTTP Client/Admin |
| PublishPromptAsync | 2 | HTTP Client/Admin |
| PublishSkillAsync | 1 | HTTP Client/Admin |
| RedraftAgentSpecAsync | 1 | HTTP Client/Admin |
| RedraftPromptAsync | 1 | HTTP Client/Admin |
| RedraftSkillAsync | 1 | HTTP Client/Admin |
| RefreshMcpToolAsync | 1 | 不支持独立 tool 操作 |
| RegisterAgentEndpointAsync | 3 | gRPC |
| RegisterAgentEndpointsAsync | 1 | gRPC |
| RegisterMcpServerEndpointAsync | 2 | gRPC |
| ReleaseAgentCardAsync | 3 | HTTP Console（public） |
| ReleaseMcpServerAsync | 2 | HTTP Console（public） |
| SearchAgentSpecsAsync | 1 | HTTP Client/Admin |
| SearchPromptsAsync | 1 | HTTP Client/Admin |
| SearchSkillsAsync | 1 | HTTP Client/Admin |
| ShutdownAsync | 1 | 完整客户端生命周期 |
| SubmitAgentSpecReviewAsync | 1 | HTTP Client/Admin |
| SubmitPromptReviewAsync | 1 | HTTP Client/Admin |
| SubmitSkillReviewAsync | 1 | HTTP Client/Admin |
| SubscribeAgentCardAsync | 2 | HTTP Console（public） |
| SubscribeAgentSpecAsync | 2 | HTTP Client/Admin |
| SubscribeMcpServerAsync | 2 | HTTP Console（public） |
| SubscribePromptAsync | 2 | HTTP Client/Admin |
| SubscribeSkillAsync | 2 | HTTP Client/Admin |
| UnsubscribeAgentCardAsync | 2 | HTTP Console（public） |
| UnsubscribeAgentSpecAsync | 2 | HTTP Client/Admin |
| UnsubscribeMcpServerAsync | 2 | HTTP Console（public） |
| UnsubscribePromptAsync | 2 | HTTP Client/Admin |
| UnsubscribeSkillAsync | 2 | HTTP Client/Admin |
| UpdateAgentSpecBizTagsAsync | 1 | HTTP Client/Admin |
| UpdateAgentSpecDraftAsync | 1 | HTTP Client/Admin |
| UpdateAgentSpecLabelsAsync | 1 | HTTP Client/Admin |
| UpdateAgentSpecScopeAsync | 1 | HTTP Client/Admin |
| UpdateMcpToolAsync | 1 | 不支持独立 tool 操作 |
| UpdatePromptBizTagsAsync | 1 | HTTP Client/Admin |
| UpdatePromptDescriptionAsync | 1 | HTTP Client/Admin |
| UpdatePromptDraftAsync | 1 | HTTP Client/Admin |
| UpdatePromptLabelsAsync | 1 | HTTP Client/Admin |
| UpdateSkillBizTagsAsync | 1 | HTTP Client/Admin |
| UpdateSkillDraftAsync | 1 | HTTP Client/Admin |
| UpdateSkillLabelsAsync | 1 | HTTP Client/Admin |
| UpdateSkillScopeAsync | 1 | HTTP Client/Admin |
| UploadAgentSpecAsync | 1 | HTTP Client/Admin |
| UploadSkillZipAsync | 1 | HTTP Client/Admin |
| ValidateImportAsync | 1 | HTTP Console（public） |
