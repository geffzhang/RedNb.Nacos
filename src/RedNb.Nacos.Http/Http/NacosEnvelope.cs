using System.Text.Json;
using RedNb.Nacos.Core;

namespace RedNb.Nacos.Client.Http;

/// <summary>
/// Helpers for the Nacos v3 JSON response envelope
/// (<c>{"code":0,"message":"success","data":...}</c>). Nacos v3 reports failures
/// inside the envelope instead of relying on a non-2xx HTTP status, so the body of
/// every response has to be inspected before a request is treated as successful.
/// </summary>
internal static class NacosEnvelope
{
    /// <summary>
    /// Parses a response body into its envelope root. Returns <c>false</c> for an
    /// empty body or for a body that is not a JSON object.
    /// </summary>
    public static bool TryParse(string? response, out JsonElement root)
    {
        root = default;

        if (string.IsNullOrEmpty(response))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Clone so the element outlives the JsonDocument.
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the envelope <c>code</c>. A missing or non-numeric code counts as
    /// <see cref="NacosConstants.SuccessCode"/>, matching the server's default.
    /// </summary>
    public static int GetCode(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("code", out var code) &&
            code.TryGetInt32(out var value))
        {
            return value;
        }

        return NacosConstants.SuccessCode;
    }

    /// <summary>
    /// Reads the envelope <c>message</c>, if present.
    /// </summary>
    public static string? GetMessage(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.String)
        {
            return message.GetString();
        }

        return null;
    }

    /// <summary>
    /// Throws a <see cref="NacosException"/> when the envelope reports a non-zero
    /// code. A body that cannot be parsed as an envelope is ignored — callers that
    /// need to reject unparseable responses check <see cref="TryParse"/> themselves.
    /// </summary>
    public static void ThrowIfFailed(string? response, string operation)
    {
        if (TryParse(response, out var root))
        {
            ThrowIfFailed(root, operation);
        }
    }

    /// <summary>
    /// Throws a <see cref="NacosException"/> when an already parsed envelope reports
    /// a non-zero code.
    /// </summary>
    public static void ThrowIfFailed(JsonElement root, string operation)
    {
        var code = GetCode(root);

        if (code != NacosConstants.SuccessCode)
        {
            throw new NacosException(NacosException.ServerError,
                $"{operation} failed with code {code}: {GetMessage(root) ?? "unknown error"}");
        }
    }
}
