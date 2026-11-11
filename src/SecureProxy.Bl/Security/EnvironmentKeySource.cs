using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SecureProxy.Bl.Security;

public sealed class EnvironmentKeySource : IKeySource
{
    private static readonly int[] ValidKeyLengths = { 16, 24, 32 };

    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    public EnvironmentKeySource(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public byte[] GetKey(string keyRef)
    {
        if (string.IsNullOrWhiteSpace(keyRef))
            throw new KeyNotFoundException("Empty key reference.");

        return _cache.GetOrAdd(keyRef, Resolve);
    }

    #region Private Methods

    private byte[] Resolve(string keyRef)
    {
        var raw = ReadRaw(keyRef);
        if (string.IsNullOrWhiteSpace(raw))
            throw new KeyNotFoundException($"Key reference could not be resolved: {keyRef}");

        var key = DecodeKey(raw);
        if (Array.IndexOf(ValidKeyLengths, key.Length) < 0)
            throw new InvalidOperationException("Resolved key must be 16, 24 or 32 bytes.");

        return key;
    }

    private string? ReadRaw(string keyRef)
    {
        var sep = keyRef.IndexOf(':');
        if (sep <= 0)
            return null;

        var scheme = keyRef[..sep];
        var name = keyRef[(sep + 1)..];
        return scheme switch
        {
            "env" => Environment.GetEnvironmentVariable(name),
            "config" => _configuration[$"Encryption:Keys:{name}"],
            _ => null
        };
    }

    private static byte[] DecodeKey(string raw)
    {
        raw = raw.Trim();

        if (TryDecodeBase64(raw, out var fromBase64))
            return fromBase64;

        if (IsHex(raw))
            return Convert.FromHexString(raw);

        return Encoding.UTF8.GetBytes(raw);
    }

    private static bool TryDecodeBase64(string raw, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(raw);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }

    private static bool IsHex(string raw)
        => raw.Length % 2 == 0 && raw.All(Uri.IsHexDigit);

    #endregion
}
