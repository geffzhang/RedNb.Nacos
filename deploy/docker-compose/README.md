# Nacos 3.2.4 单机测试部署

默认使用内置存储和鉴权，数据/日志持久化；端口仅绑定本机。首次在本目录生成 .env（后续保留原文件）：

```bash
umask 077
printf 'NACOS_AUTH_TOKEN=%s\nNACOS_AUTH_IDENTITY_VALUE=%s\n' "$(openssl rand -base64 48 | tr -d '\n')" "$(openssl rand -hex 24)" > .env
docker compose up -d
docker compose ps
```

访问 http://localhost:8080 初始化 nacos 管理员密码。SDK 使用 8848，客户端 gRPC 使用 9848。需要从其他机器访问时，在 .env 设置 NACOS_BIND_ADDRESS 为服务器内网地址并按需放行端口。

CI 使用临时容器和测试账户；真实凭据不入库。mysql/cluster compose 文件保留为备选示例，未纳入本次单机验收，不应直接视为已经验证的生产部署。
