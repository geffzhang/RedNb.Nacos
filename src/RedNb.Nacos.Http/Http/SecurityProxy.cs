using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core;

namespace RedNb.Nacos.Client.Http;

/// <summary>
/// Handles authentication with Nacos server.
///
/// Strategy selection (see <see cref="ResolveLoginStrategy"/>):
/// <list type="bullet">
///   <item>AccessKey + SecretKey both set → AK/SK signed login (preferred when both are set)</item>
///   <item>Username + Password both set → legacy <c>/v3/auth/user/login</c> form-body login</item>
///   <item>neither → <see cref="NacosException"/> with <see cref="NacosException.InvalidParam"/></item>
/// </list>
///
/// The JWT token cache + TTL refresh logic is strategy-agnostic: whichever
/// login path produced the token is cached, reused, and refreshed the same
/// way. gRPC's <c>Payload.Metadata.Headers["accessToken"]</c> injection reads
/// the cached token via <see cref="GetAccessTokenAsync"/> and so transparently
/// picks up whichever flavor was obtained.
/// </summary>
public class SecurityProxy : IDisposable
{
    private readonly NacosClientOptions _options;
    private readonly ILogger? _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    private string? _accessToken;
    private long _tokenTtl;
    private long _lastRefreshTime;
    private bool _disposed;

    private const long TokenRefreshWindow = 120000; // 2 minutes before expiry

    public SecurityProxy(NacosClientOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(options.DefaultTimeout)
        };
    }

    /// <summary>
    /// Gets the access token, refreshing if necessary.
    /// Returns <c>null</c> only when neither AccessKey/SecretKey nor
    /// Username/Password is configured (anonymous access).
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var loginStrategy = ResolveLoginStrategy();
        if (loginStrategy == LoginStrategy.None)
        {
            return null;
        }

        if (IsTokenValid())
        {
            return _accessToken;
        }

        await _loginLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (IsTokenValid())
            {
                return _accessToken;
            }

            await LoginAsync(loginStrategy, cancellationToken);
            return _accessToken;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private bool IsTokenValid()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            return false;
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expiryTime = _lastRefreshTime + (_tokenTtl * 1000) - TokenRefreshWindow;
        return currentTime < expiryTime;
    }

    private LoginStrategy ResolveLoginStrategy()
    {
        var hasAk = !string.IsNullOrWhiteSpace(_options.AccessKey)
            && !string.IsNullOrWhiteSpace(_options.SecretKey);
        var hasUp = !string.IsNullOrWhiteSpace(_options.Username)
            && !string.IsNullOrWhiteSpace(_options.Password);
        // AK/SK wins when both are configured — it is the server's preferred
        // (and more secure) path. Operators that need username/password only
        // should leave AccessKey/SecretKey null/blank.
        if (hasAk) return LoginStrategy.AccessKey;
        if (hasUp) return LoginStrategy.UsernamePassword;
        return LoginStrategy.None;
    }

    private async Task LoginAsync(LoginStrategy strategy, CancellationToken cancellationToken)
    {
        switch (strategy)
        {
            case LoginStrategy.AccessKey:
                await LoginWithAccessKeyAsync(cancellationToken);
                break;
            case LoginStrategy.UsernamePassword:
                await LoginWithUsernamePasswordAsync(cancellationToken);
                break;
            default:
                throw new NacosException(
                    NacosException.InvalidParam,
                    "Either AccessKey/SecretKey or Username/Password must be configured");
        }
    }

    private async Task LoginWithUsernamePasswordAsync(CancellationToken cancellationToken)
    {
        var servers = _options.GetServerAddressList();
        Exception? lastException = null;

        foreach (var server in servers)
        {
            try
            {
                var baseUrl = _options.GetBaseUrl(server);
                var loginUrl = $"{baseUrl}/v3/auth/user/login";

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "username", _options.Username! },
                    { "password", _options.Password! }
                });

                var response = await _httpClient.PostAsync(loginUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseBody);
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
                    {
                        _accessToken = loginResponse.AccessToken;
                        _tokenTtl = loginResponse.TokenTtl;
                        _lastRefreshTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        _logger?.LogDebug("Successfully logged in to Nacos server, token TTL: {Ttl}s", _tokenTtl);
                        return;
                    }
                }

                _logger?.LogWarning("Login failed for server {Server}: {Response}", server, responseBody);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Login failed for server {Server}", server);
            }
        }

        throw new NacosException(NacosException.NoRight,
            $"Failed to login to Nacos server: {lastException?.Message}", lastException!);
    }

    private async Task LoginWithAccessKeyAsync(CancellationToken cancellationToken)
    {
        var servers = _options.GetServerAddressList();
        // Sign once — the signature is timestamp-bound and the server validates
        // it server-side. We pick the timestamp at the start of the login
        // attempt so all servers in the list see the same signed value.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = SignatureUtils.SignRequest(
            _options.AccessKey!, _options.SecretKey!, timestamp);

        Exception? lastException = null;

        foreach (var server in servers)
        {
            try
            {
                var baseUrl = _options.GetBaseUrl(server);
                // The server's default-auth-plugin only exposes a single login
                // route (/v3/auth/user/login); it dispatches on which params are
                // present. For AK/SK we send the signed query string and an
                // empty body.
                var loginUrl =
                    $"{baseUrl}/v3/auth/user/login" +
                    $"?accessKey={Uri.EscapeDataString(_options.AccessKey!)}" +
                    $"&timestamp={Uri.EscapeDataString(timestamp)}" +
                    $"&signature={Uri.EscapeDataString(signature)}";

                var response = await _httpClient.PostAsync(loginUrl, content: null, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseBody);
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.AccessToken))
                    {
                        _accessToken = loginResponse.AccessToken;
                        _tokenTtl = loginResponse.TokenTtl;
                        _lastRefreshTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        _logger?.LogDebug("Successfully logged in to Nacos server with AK/SK, token TTL: {Ttl}s", _tokenTtl);
                        return;
                    }
                }

                _logger?.LogWarning("AK/SK login failed for server {Server}: {Response}", server, responseBody);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "AK/SK login failed for server {Server}", server);
            }
        }

        throw new NacosException(NacosException.NoRight,
            $"Failed to login to Nacos server with AK/SK: {lastException?.Message}", lastException!);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _loginLock.Dispose();
        _disposed = true;
    }

    private enum LoginStrategy
    {
        None,
        UsernamePassword,
        AccessKey
    }

    private class LoginResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("tokenTtl")]
        public long TokenTtl { get; set; }

        [JsonPropertyName("globalAdmin")]
        public bool GlobalAdmin { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}

/// <summary>
/// Nacos AK/SK signing helpers.
///
/// <see cref="SignRequest"/> produces the canonical query-string signature
/// expected by the Nacos 2.x/3.x server's <c>default-auth-plugin</c>:
/// <c>Base64(HMAC-SHA1(secretKey, accessKey + timestamp))</c>. The
/// sign-string shape matches the Java client's
/// <c>com.alibaba.nacos.client.security.NacosAuthLoginServiceImpl.signRequest</c>
/// for the login route; the signing key/algorithm are the same that the
/// server validates with.
/// </summary>
public static class SignatureUtils
{
    /// <summary>
    /// Computes the AK/SK signature for a login request.
    /// </summary>
    /// <param name="accessKey">The configured AccessKey.</param>
    /// <param name="secretKey">The configured SecretKey.</param>
    /// <param name="timestamp">13-digit unix-millis timestamp as a string.</param>
    /// <returns>Base64-encoded HMAC-SHA1 signature.</returns>
    public static string SignRequest(string accessKey, string secretKey, string timestamp)
    {
        if (string.IsNullOrEmpty(accessKey)) throw new ArgumentException("accessKey is required", nameof(accessKey));
        if (string.IsNullOrEmpty(secretKey)) throw new ArgumentException("secretKey is required", nameof(secretKey));
        if (string.IsNullOrEmpty(timestamp)) throw new ArgumentException("timestamp is required", nameof(timestamp));

        var data = Encoding.UTF8.GetBytes(accessKey + timestamp);
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }
}