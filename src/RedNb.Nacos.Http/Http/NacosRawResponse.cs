using System.Text;

namespace RedNb.Nacos.Client.Http;

/// <summary>
/// Raw HTTP response from the Nacos server, exposing the status code,
/// response headers and binary body. Used by APIs that rely on conditional
/// requests (304), binary downloads or custom response headers.
/// </summary>
public class NacosRawResponse
{
    /// <summary>
    /// Creates a raw response.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="headers">Response headers (case-insensitive keys).</param>
    /// <param name="body">Response body bytes.</param>
    public NacosRawResponse(int statusCode, IReadOnlyDictionary<string, string> headers, byte[] body)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
    }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Response headers with case-insensitive keys.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Response body bytes.
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// Response body decoded as a UTF-8 string.
    /// </summary>
    public string BodyString => Encoding.UTF8.GetString(Body);

    /// <summary>
    /// Whether the status is 304 (Not Modified).
    /// </summary>
    public bool IsNotModified => StatusCode == 304;

    /// <summary>
    /// Gets a response header value, or null when absent.
    /// </summary>
    /// <param name="name">Header name (case-insensitive).</param>
    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : null;
    }
}
