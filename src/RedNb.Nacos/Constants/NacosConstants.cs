namespace RedNb.Nacos;

/// <summary>
/// Common constants used throughout the Nacos SDK.
/// </summary>
public static class NacosConstants
{
    /// <summary>Wire client identity, derived from the SDK assembly version.</summary>
    public static string ClientVersion { get; } = "RedNb.Nacos/" + (typeof(NacosConstants).Assembly.GetName().Version?.ToString(3) ?? "unknown");

    /// <summary>
    /// Default group name.
    /// </summary>
    public const string DefaultGroup = "DEFAULT_GROUP";

    /// <summary>
    /// Default cluster name.
    /// </summary>
    public const string DefaultClusterName = "DEFAULT";

    /// <summary>
    /// Default namespace (public).
    /// </summary>
    public const string DefaultNamespace = "public";

    /// <summary>
    /// Service info splitter.
    /// </summary>
    public const string ServiceInfoSplitter = "@@";

    /// <summary>
    /// Default heart beat interval in milliseconds.
    /// </summary>
    public const long DefaultHeartBeatInterval = 5000;

    /// <summary>
    /// Default heart beat timeout in milliseconds.
    /// </summary>
    public const long DefaultHeartBeatTimeout = 15000;

    /// <summary>
    /// Default IP delete timeout in milliseconds.
    /// </summary>
    public const long DefaultIpDeleteTimeout = 30000;

    /// <summary>
    /// Default instance ID generator type.
    /// </summary>
    public const string DefaultInstanceIdGenerator = "simple";

    /// <summary>
    /// NULL string constant.
    /// </summary>
    public const string Null = "null";

    /// <summary>
    /// ALL pattern for fuzzy matching.
    /// </summary>
    public const string AllPattern = "*";

    /// <summary>
    /// Any pattern for fuzzy matching.
    /// </summary>
    public const string AnyPattern = "*";

    /// <summary>
    /// Default encoding.
    /// </summary>
    public const string DefaultEncoding = "UTF-8";

    /// <summary>
    /// Config module.
    /// </summary>
    public const string ConfigModule = "config";

    /// <summary>
    /// Naming module.
    /// </summary>
    public const string NamingModule = "naming";

    /// <summary>
    /// Client version header.
    /// </summary>
    public const string ClientVersionHeader = "Client-Version";

    /// <summary>
    /// User agent header.
    /// </summary>
    public const string UserAgentHeader = "User-Agent";

    /// <summary>
    /// Request source header.
    /// </summary>
    public const string RequestSourceHeader = "Request-Source";

    /// <summary>
    /// Content type.
    /// </summary>
    public const string ContentType = "Content-Type";

    /// <summary>
    /// Content type form urlencoded.
    /// </summary>
    public const string ContentTypeFormUrlEncoded = "application/x-www-form-urlencoded";

    /// <summary>
    /// Content type JSON.
    /// </summary>
    public const string ContentTypeJson = "application/json";

    /// <summary>
    /// Default timeout in milliseconds.
    /// </summary>
    public const int DefaultTimeout = 3000;

    /// <summary>
    /// Default long poll timeout in milliseconds.
    /// </summary>
    public const int DefaultLongPollTimeout = 30000;

    /// <summary>
    /// Access token header.
    /// </summary>
    public const string AccessToken = "accessToken";

    /// <summary>
    /// Token TTL header.
    /// </summary>
    public const string TokenTtl = "tokenTtl";

    /// <summary>
    /// Token refresh window.
    /// </summary>
    public const long TokenRefreshWindow = 120000;

    /// <summary>
    /// Nacos server protocol version 3.
    /// </summary>
    public const string ProtocolV3 = "v3";

    /// <summary>
    /// Success code of the Nacos v3 response envelope.
    /// </summary>
    public const int SuccessCode = 0;

    /// <summary>
    /// Nacos v3 response code indicating the requested config does not exist.
    /// </summary>
    public const int ConfigNotFoundCode = 20004;

    /// <summary>
    /// Legacy HTTP header used to pass <c>namespaceId</c> to the Nacos console AI
    /// endpoints (Prompt / Skill / AgentSpec), which are parked and not exercised by
    /// the live integration suite. It is retained for those endpoints only: the v3
    /// client and admin APIs this library targets pass <c>namespaceId</c> as a request
    /// parameter instead (and the v3 spec neither requires nor defines this header).
    /// See https://nacos.io/en/docs/v3/open-api for the v3 spec.
    /// </summary>
    public const string NamespaceHeader = "X-Nacos-Namespace-Id";
}
