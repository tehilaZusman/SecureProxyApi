using System.Net;
using System.Net.Sockets;
using System.Net.Http;

namespace SecureProxy.Api.Security;

public static class SecureUpstreamConnection
{
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken,
        bool allowPrivateNetworks)
    {
        var host = context.DnsEndPoint.Host;
        IPAddress[] resolved;
        if (IPAddress.TryParse(host, out var literal))
            resolved = new[] { literal };
        else
            resolved = await Dns.GetHostAddressesAsync(host, cancellationToken);

        var candidates = resolved.Where(ip => IsAllowed(ip, allowPrivateNetworks)).ToArray();
        if (candidates.Length == 0)
            throw new HttpRequestException("The configured upstream resolved to a prohibited network address.");

        Exception? lastError = null;
        foreach (var address in candidates)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                lastError = ex;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Could not connect to the configured upstream.", lastError);
    }

    public static bool IsAllowed(IPAddress address, bool allowPrivateNetworks)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast || address.IsIPv6SiteLocal || address.IsIPv6Teredo)
            return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var a = bytes[0]; var b = bytes[1];
            if (a == 0 || a >= 224 || (a == 169 && b == 254)) return false;
            if ((a == 100 && b >= 64 && b <= 127)
                || (a == 192 && b == 0 && (bytes[2] == 0 || bytes[2] == 2))
                || (a == 192 && b == 88 && bytes[2] == 99)
                || (a == 198 && (b == 18 || b == 19))
                || (a == 198 && b == 51 && bytes[2] == 100)
                || (a == 203 && b == 0 && bytes[2] == 113)) return false;
            var isPrivate = a == 10 || (a == 172 && b >= 16 && b <= 31) || (a == 192 && b == 168);
            return allowPrivateNetworks || !isPrivate;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;
            var isDocumentation = bytes[0] == 0x20 && bytes[1] == 0x01
                && bytes[2] == 0x0D && bytes[3] == 0xB8;
            return !isDocumentation && (allowPrivateNetworks || !isUniqueLocal);
        }

        return false;
    }
}
