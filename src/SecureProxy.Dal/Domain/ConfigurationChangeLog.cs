namespace SecureProxy.Dal.Domain;

public class ConfigurationChangeLog
{
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? ChangedBy { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
