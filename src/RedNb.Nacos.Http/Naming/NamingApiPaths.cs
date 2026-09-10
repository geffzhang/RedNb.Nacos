namespace RedNb.Nacos.Http.Naming;

/// <summary>
/// HTTP v3 API path constants for the Nacos naming service.
/// All paths are server-relative (no leading host) and match the
/// Nacos 3.x OpenAPI v3 spec at https://nacos.io/en/docs/v3/open-api
/// </summary>
internal static class NamingApiPaths
{
    /// <summary>Base path for naming v3 endpoints.</summary>
    public const string Base = "/v3/ns";

    /// <summary>Register / query / update / delete / patch a single instance.</summary>
    public const string Instance = "/v3/ns/instance";

    /// <summary>List instances of a service.</summary>
    public const string InstanceList = "/v3/ns/instance/list";

    /// <summary>List services in a namespace/group.</summary>
    public const string ServiceList = "/v3/ns/service/list";

    /// <summary>Health-check a specific instance.</summary>
    public const string Health = "/v3/ns/health/instance";

    /// <summary>Send a heartbeat (BeatReactor).</summary>
    public const string Beat = "/v3/ns/instance/beat";

    /// <summary>
    /// Nacos v3 sends namespaceId as an HTTP header, not a query parameter.
    /// See https://nacos.io/en/docs/v3/open-api for the v3 spec.
    /// </summary>
    public const string NamespaceHeader = "X-Nacos-Namespace-Id";

    // Aliases preserved for v1 paths still referenced elsewhere in the codebase
    // (e.g., legacy failover paths). Phase 5 cleanup will remove these.
    internal const string LegacyInstance = "v1/ns/instance";
    internal const string LegacyInstanceList = "v1/ns/instance/list";
    internal const string LegacyServiceList = "v1/ns/service/list";
    internal const string LegacyHealth = "v1/ns/health/instance";
    internal const string LegacyBeat = "v1/ns/instance/beat";
}
