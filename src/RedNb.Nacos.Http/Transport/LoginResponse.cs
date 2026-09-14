using System.Text.Json.Serialization;

namespace RedNb.Nacos.Http.Transport;

internal sealed class LoginResponse
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
