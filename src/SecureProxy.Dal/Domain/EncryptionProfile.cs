namespace SecureProxy.Dal.Domain;

public class EncryptionProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "AES-GCM";

    public string KeyRef { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
