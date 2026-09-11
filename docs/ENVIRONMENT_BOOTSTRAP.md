# Bootstrap a Nacos 3.2.4 dev environment for SDK testing

The SDK integration tests assume a running Nacos 3.2.4 with:
- HTTP API on `localhost:8848` (gRPC on `9848`)
- Console on `localhost:8080` (separate listener, separate auth scope)
- A bootstrapped `nacos` user (no API/UI to create one exists in 3.2.4)

## 1. docker compose up

```bash
cd deploy/docker-compose
docker compose up -d nacos
```

Wait for readiness:

```bash
curl -fsS http://localhost:8080/v3/console/health/readiness
# → 200 once ready (≈18s on a clean Derby)
```

## 2. Bootstrap the `nacos` admin user

Nacos 2.2.2+ does not auto-create a default user, and 3.2.4 has no
user-management API or UI. Insert directly into Derby.

The container is JRE-only; build the bootstrap JARs on the host. The two
Spring jars come out of the `nacos-server` fat jar, but the Derby engine does
**not** live in the fat jar — it ships as a server plugin, so copy it from the
running container:

```bash
# Derby engine — from the container's plugins directory:
docker cp nacos-server:/home/nacos/plugins/derby-10.14.2.0.jar .

# spring-security-crypto + spring-jcl — from the nacos-server fat jar:
python -c "
import zipfile, os
outer = zipfile.ZipFile('nacos-server.jar')
for n in outer.namelist():
    if not n.endswith('.jar'): continue
    if not any(x in n for x in ('spring-security-crypto', 'spring-jcl')): continue
    with open(os.path.join('.', os.path.basename(n)), 'wb') as out:
        out.write(outer.read(n))
"
```

Write `InsertNacosUser.java`:

```java
import java.sql.*;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;

public class InsertNacosUser {
    public static void main(String[] args) throws Exception {
        String dbPath = args[0]; String user = args[1]; String pass = args[2];
        String hash = new BCryptPasswordEncoder().encode(pass);
        try (Connection c = DriverManager.getConnection("jdbc:derby:" + dbPath + ";create=false")) {
            try (PreparedStatement p = c.prepareStatement(
                    "INSERT INTO NACOS.users (username, password, enabled) VALUES (?, ?, true)")) {
                p.setString(1, user); p.setString(2, hash); p.executeUpdate();
            }
            System.out.println("inserted user " + user);
            try (PreparedStatement p = c.prepareStatement(
                    "INSERT INTO NACOS.roles (username, role) VALUES (?, 'ROLE_ADMIN')")) {
                p.setString(1, user); p.executeUpdate();
            }
            System.out.println("granted ROLE_ADMIN to " + user);
            c.commit();
        } finally {
            try { DriverManager.getConnection("jdbc:derby:;shutdown=true"); } catch (SQLException ignored) {}
        }
    }
}
```

Build and run against the container's Derby directory (mounted to
`./nacos/data/derby-data`):

```bash
javac InsertNacosUser.java
java -cp "spring-security-crypto-6.5.10.jar;spring-jcl-6.2.18.jar;derby-10.14.2.0.jar;." \
  InsertNacosUser "deploy/docker-compose/nacos/data/derby-data" nacos nacos
# → inserted user nacos
#    granted ROLE_ADMIN to nacos
```

> Working directory and platform: the `javac`/`java` commands assume the three
> jars are in the current directory (where the `docker cp` / `python` steps
> above put them); the Derby path argument is relative to the repo root, so
> pass an absolute path if you run from elsewhere. The classpath separator
> shown is `;` (Windows) — use `:` on Linux/macOS. `mv`/`$(date)` in §4 assume
> a bash-style shell.

Restart the container so the in-memory user cache picks up the new row:

```bash
docker compose restart nacos
# wait for readiness again
```

## 3. Verify

```bash
# Login (uses 8848; token works on both ports)
TOKEN=$(curl -s -X POST http://localhost:8848/nacos/v3/auth/user/login \
  -d 'username=nacos&password=nacos' \
  | python -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

# Console AI list (8080, console-auth scoped)
curl -s "http://localhost:8080/v3/console/ai/mcp/list?accessToken=$TOKEN"
# → {"code":0,"message":"success","data":{"totalCount":0,...}}
```

## 4. Resetting the environment

The Derby data dir may become corrupt across unclean restarts (ExitCode=1
crash-loops with `load derby-schema.sql error`). To recover, rename the
corrupted dir aside and let compose recreate:

```bash
cd deploy/docker-compose
mv nacos/data/derby-data nacos/data/derby-data.bak-$(date +%Y%m%d)
docker compose restart nacos   # re-bootstraps Derby
# → then redo step 2 to re-insert the nacos user
```

## 5. SDK options for this environment

```csharp
var options = new NacosClientOptions
{
    ServerAddresses  = "localhost:8848",
    ConsoleAddresses = "localhost:8080",
    Username         = "nacos",
    Password         = "nacos",
    EnableGrpc       = true,
    DefaultTimeout   = 10000
};
```

Entry points (verified against `src/RedNb.Nacos.Http/NacosFactory.cs` and
`src/RedNb.Nacos.Grpc/NacosGrpcFactory.cs`):

```csharp
// HTTP AI service (console ports for the MCP/A2A admin API).
IAiService httpAi = new NacosFactory().CreateAiService(options);

// gRPC AI service — the async factory initializes the channel for you.
IAiService grpcAi = await NacosGrpcFactory.CreateAiServiceAsync(options);
```

The instance form `new NacosGrpcFactory().CreateAiService(options)` does *not*
initialize the channel; call `InitializeAsync()` on the returned
`NacosGrpcAiService` yourself if you use it.

Note: the JSON sample in `deploy/docker-compose/README.md` uses `"UseGrpc"`,
but the C# property is `EnableGrpc` — use the C# name in code.
