using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public static class HeaderPolicy
{
    private static readonly HashSet<string> AlwaysBlocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "X-Proxy-Api-Key",
        "X-Admin-Api-Key",
        "Cookie",
        "Set-Cookie",
        "Connection",
        "Keep-Alive",
        "Proxy-Connection",
        "Transfer-Encoding",
        "TE",
        "Trailer",
        "Upgrade",
        "Host",
        "Content-Length"
    };

    public static IReadOnlyDictionary<string, string> Filter(
        Operation operation,
        IEnumerable<KeyValuePair<string, string>> incoming)
    {
        var allow = new HashSet<string>(
            operation.HeaderRules.Select(r => r.HeaderName),
            StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in incoming)
        {
            if (AlwaysBlocked.Contains(kv.Key))
                continue;
            if (!allow.Contains(kv.Key))
                continue;
            result[kv.Key] = kv.Value;
        }
        return result;
    }

    public static bool IsAlwaysBlocked(string headerName) => AlwaysBlocked.Contains(headerName);
}
