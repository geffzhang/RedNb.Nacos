using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using RedNb.Nacos.Client.Http;
using RedNb.Nacos.Core;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace RedNb.Nacos.Http.Tests.Auth;

/// <summary>
/// Tests for <see cref="SecurityProxy"/> credential handling.
///
/// Covers the AK/SK login branch (signed query-string), the username/password
/// form-body branch (regression), the AK/SK precedence rule, and the
/// no-credentials path. Each test stubs out the Nacos server with WireMock
/// and inspects the captured request to assert the outgoing shape.
/// </summary>
public class SecurityProxyTests : IDisposable
{
    private readonly WireMockServer _server;

    public SecurityProxyTests()
    {
        _server = WireMockServer.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithAccessKeySecretKey_SignsRequestAndReturnsToken()
    {
        // Arrange
        const string accessKey = "ak";
        const string secretKey = "sk";
        var options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}",
            AccessKey = accessKey,
            SecretKey = secretKey
        };

        // WireMock will match the AK/SK-shaped URL: a POST to /v3/auth/user/login
        // with the accessKey query param. If the request shape is wrong, this
        // stub won't match and WireMock will return a 404 — the test fails.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/auth/user/login")
                .WithParam("accessKey", accessKey)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"accessToken\":\"ak-token\",\"tokenTtl\":18000}"));

        var proxy = new SecurityProxy(options);

        // Act
        var token = await proxy.GetAccessTokenAsync();

        // Assert: we got the token back
        token.Should().Be("ak-token");

        // Assert: exactly one login was sent and it was the AK/SK form
        _server.LogEntries.Should().HaveCount(1);
        var entry = _server.LogEntries.Single();
        var urlString = entry.RequestMessage.Url;
        var captured = ParseUrl(urlString);
        captured.AbsolutePath.Should().Be("/nacos/v3/auth/user/login");

        var queryParams = ParseQueryString(captured.Query);
        queryParams.Should().ContainKey("accessKey").WhoseValue.Should().Be(accessKey);
        queryParams.Should().ContainKey("timestamp");
        queryParams.Should().ContainKey("signature");

        // Assert: signature = Base64(HMAC-SHA1(secretKey, accessKey + timestamp))
        var timestamp = queryParams["timestamp"];
        var expectedSignature = ComputeExpectedSignature(accessKey, secretKey, timestamp);
        queryParams["signature"].Should().Be(expectedSignature,
            "the AK/SK signature must be Base64(HMAC-SHA1(secretKey, accessKey + timestamp))");

        // Assert: no form body was sent
        entry.RequestMessage.Body.Should().BeNullOrEmpty(
            "the AK/SK login path encodes everything in the query string, not the body");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithUsernamePassword_SendsFormBody()
    {
        // Arrange
        var options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}",
            Username = "alice",
            Password = "wonderland"
        };

        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/auth/user/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"accessToken\":\"up-token\",\"tokenTtl\":18000}"));

        var proxy = new SecurityProxy(options);

        // Act
        var token = await proxy.GetAccessTokenAsync();

        // Assert: token returned
        token.Should().Be("up-token");

        // Assert: a single login was sent with the form body
        _server.LogEntries.Should().HaveCount(1);
        var entry = _server.LogEntries.Single();
        var body = entry.RequestMessage.Body;
        body.Should().NotBeNullOrEmpty();
        body.Should().Contain("username=alice")
            .And.Contain("password=wonderland");

        // Assert: the URL has no AK/SK query params (regression guard)
        var upCaptured = ParseUrl(entry.RequestMessage.Url);
        upCaptured.Query.Should().NotContain("accessKey");
        upCaptured.Query.Should().NotContain("signature");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithBothCredentialSets_AkSkWins()
    {
        // Arrange
        var options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}",
            AccessKey = "ak",
            SecretKey = "sk",
            Username = "alice",
            Password = "wonderland"
        };

        // Match only the AK/SK-shaped URL. If the proxy chose username/password
        // instead, the request would be missing `accessKey` and this stub would
        // not match — WireMock returns 404 and the test fails.
        _server
            .Given(Request.Create()
                .WithPath("/nacos/v3/auth/user/login")
                .WithParam("accessKey", "ak")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"accessToken\":\"precedence-token\",\"tokenTtl\":18000}"));

        var proxy = new SecurityProxy(options);

        // Act
        var token = await proxy.GetAccessTokenAsync();

        // Assert
        token.Should().Be("precedence-token");
        _server.LogEntries.Should().HaveCount(1,
            "exactly one login request should fire; AK/SK must short-circuit username/password");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithNoCredentials_ReturnsNullWithoutHttpCall()
    {
        // Arrange
        var options = new NacosClientOptions
        {
            ServerAddresses = $"localhost:{_server.Port}"
            // no Username/Password and no AccessKey/SecretKey
        };

        var proxy = new SecurityProxy(options);

        // Act
        var token = await proxy.GetAccessTokenAsync();

        // Assert: anonymous mode — no login attempted, no token returned
        token.Should().BeNull();
        _server.LogEntries.Should().BeEmpty(
            "GetAccessTokenAsync must skip the HTTP round-trip when there is nothing to authenticate with");
    }

    [Fact]
    public void Validate_WithAkSkButNoServerAddresses_ThrowsInvalidParam()
    {
        // Arrange
        var options = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = "console.example:8080", // console-only, no core
            AccessKey = "ak",
            SecretKey = "sk"
        };

        // Act
        var act = () => options.Validate();

        // Assert: same gate as the existing username/password rule
        act.Should().Throw<NacosException>()
            .Where(e => e.ErrorCode == NacosException.InvalidParam)
            .WithMessage("*AccessKey/SecretKey*ServerAddresses*");
    }

    [Fact]
    public void Validate_WithUsernamePasswordButNoServerAddresses_StillThrowsInvalidParam()
    {
        // Regression: existing rule must still fire when only U/P is set.
        var options = new NacosClientOptions
        {
            ServerAddresses = "",
            ConsoleAddresses = "console.example:8080",
            Username = "alice",
            Password = "wonderland"
        };

        var act = () => options.Validate();

        act.Should().Throw<NacosException>()
            .Where(e => e.ErrorCode == NacosException.InvalidParam)
            .WithMessage("*Username/Password*ServerAddresses*");
    }

    [Fact]
    public void Validate_WithAkSkAndServerAddresses_DoesNotThrow()
    {
        // Arrange
        var options = new NacosClientOptions
        {
            ServerAddresses = "localhost:8848",
            AccessKey = "ak",
            SecretKey = "sk"
        };

        // Act + Assert
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void SignatureUtils_SignRequest_ReturnsDeterministicBase64HmacSha1()
    {
        // Arrange
        const string accessKey = "ak";
        const string secretKey = "sk";
        const string timestamp = "1700000000000";

        // Act
        var sig = SignatureUtils.SignRequest(accessKey, secretKey, timestamp);

        // Assert: matches an independent HMAC-SHA1 + Base64 computation
        var expected = ComputeExpectedSignature(accessKey, secretKey, timestamp);
        sig.Should().Be(expected);
        sig.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SignatureUtils_SignRequest_WithDifferentTimestamps_YieldsDifferentSignatures()
    {
        var s1 = SignatureUtils.SignRequest("ak", "sk", "1700000000000");
        var s2 = SignatureUtils.SignRequest("ak", "sk", "1700000000001");
        s1.Should().NotBe(s2);
    }

    [Fact]
    public void SignatureUtils_SignRequest_WithMissingArgs_Throws()
    {
        FluentActions.Invoking(() => SignatureUtils.SignRequest("", "sk", "ts"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SignatureUtils.SignRequest("ak", "", "ts"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SignatureUtils.SignRequest("ak", "sk", ""))
            .Should().Throw<ArgumentException>();
    }

    private static string ComputeExpectedSignature(string accessKey, string secretKey, string timestamp)
    {
        var data = Encoding.UTF8.GetBytes(accessKey + timestamp);
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    private static Uri ParseUrl(string urlString)
    {
        // WireMock's RequestMessage.Url can be "host:port/path?query" or
        // "http://host:port/path?query" depending on the version. Normalize to
        // an absolute Uri by adding a scheme when missing.
        if (urlString.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || urlString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(urlString);
        }
        if (urlString.StartsWith("//"))
        {
            return new Uri("http:" + urlString);
        }
        return new Uri("http://" + urlString);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query)) return result;
        // WireMock includes the leading '?' in the query; strip it.
        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..idx]);
                var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
                result[key] = value;
            }
        }
        return result;
    }
}