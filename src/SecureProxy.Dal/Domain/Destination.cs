namespace SecureProxy.Dal.Domain;

public class Destination
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int TimeoutMs { get; set; } = 30_000;

    public string? AllowedPorts { get; set; }

    public string? AuthProfileRef { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[]? RowVersion { get; set; }

    public ICollection<Operation> Operations { get; set; } = new List<Operation>();
}
