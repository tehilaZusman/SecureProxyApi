namespace SecureProxy.Bl.Proxy;

public sealed class ProxyResult
{
    public bool ForwardingOccurred { get; init; }

    public int ProxyStatusCode { get; init; }

    public int? UpstreamStatusCode { get; init; }

    public string ReasonCode { get; init; } = ReasonCodes.InternalError;

    public string Message { get; init; } = string.Empty;

    public byte[]? ResponseBody { get; init; }

    public string? ResponseContentType { get; init; }

    public bool IsRejection => !ForwardingOccurred && ProxyStatusCode < 500
        && ReasonCode != ReasonCodes.Forwarded;

    public static ProxyResult Reject(int status, string reasonCode, string message) => new()
    {
        ForwardingOccurred = false,
        ProxyStatusCode = status,
        ReasonCode = reasonCode,
        Message = message
    };

    public static ProxyResult ProxyFailure(int status, string reasonCode, string message) => new()
    {
        ForwardingOccurred = false,
        ProxyStatusCode = status,
        ReasonCode = reasonCode,
        Message = message
    };

    public static ProxyResult ForwardedResult(int proxyStatus, int upstreamStatus, byte[] body, string? contentType) => new()
    {
        ForwardingOccurred = true,
        ProxyStatusCode = proxyStatus,
        UpstreamStatusCode = upstreamStatus,
        ReasonCode = ReasonCodes.Forwarded,
        Message = "forwarded",
        ResponseBody = body,
        ResponseContentType = contentType
    };
}
