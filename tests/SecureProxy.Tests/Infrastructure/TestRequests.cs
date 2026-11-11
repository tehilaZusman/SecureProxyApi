using System.Text.Json;
using SecureProxy.Bl.Dtos;

namespace SecureProxy.Tests.Infrastructure;

public static class TestRequests
{
    public static ProxyExecuteRequest Build(string clientId, object? parameters = null,
        string? encryptedPayload = null)
    {
        Dictionary<string, JsonElement>? dict = null;
        if (parameters is not null)
        {
            var json = JsonSerializer.Serialize(parameters);
            dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }

        return new ProxyExecuteRequest
        {
            ClientId = clientId,
            Parameters = dict,
            EncryptedPayload = encryptedPayload
        };
    }

    public static readonly IReadOnlyList<KeyValuePair<string, string>> NoHeaders =
        Array.Empty<KeyValuePair<string, string>>();
}
