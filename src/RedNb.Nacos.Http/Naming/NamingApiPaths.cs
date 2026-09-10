namespace RedNb.Nacos.Http.Naming;

/// <summary>
/// HTTP v3 API path constants for the Nacos naming service.
/// All paths are server-relative (no leading host) and match the
/// Nacos 3.x OpenAPI v3 spec at https://nacos.io/en/docs/v3/open-api
/// </summary>
internal static class NamingApiPaths
{
    /// <summary>Register / heartbeat / deregister a single instance (client API).</summary>
    public const string Instance = "/v3/client/ns/instance";

    /// <summary>List instances of a service (client API).</summary>
    public const string InstanceList = "/v3/client/ns/instance/list";

    /// <summary>List services in a namespace/group (admin API; token auto-attached).</summary>
    public const string ServiceList = "/v3/admin/ns/service/list";
}
