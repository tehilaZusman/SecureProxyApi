namespace SecureProxy.Dal.Domain;

public enum ExecutionOutcome
{
    Rejected = 0,
    Forwarded = 1,
    ProxyError = 2
}

public class RequestLog
{
    public long Id { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string? OperationKey { get; set; }

    public int? DestinationId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long DurationMs { get; set; }

    public bool ForwardingOccurred { get; set; }

    public int? UpstreamStatusCode { get; set; }

    public int ProxyStatusCode { get; set; }

    public ExecutionOutcome Outcome { get; set; }

    public string? ReasonCode { get; set; }
}
