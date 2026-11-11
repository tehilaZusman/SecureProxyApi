using System.Text.Json;

namespace SecureProxy.Bl.Dtos;

public class ProxyExecuteRequest
{
    public string? ClientId { get; set; }

    public string? UrlId { get; set; }

    public Dictionary<string, JsonElement>? Parameters { get; set; }

    public string? EncryptedPayload { get; set; }
}
