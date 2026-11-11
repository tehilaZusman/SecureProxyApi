namespace SecureProxy.Dal.Domain;

public class Operation
{
    public int Id { get; set; }

    public string OperationKey { get; set; } = string.Empty;

    public int DestinationId { get; set; }
    public Destination Destination { get; set; } = null!;

    public string HttpMethod { get; set; } = "GET";

    public string RelativePathTemplate { get; set; } = "/";

    public bool IsActive { get; set; } = true;

    public long MaxRequestBytes { get; set; } = 1024 * 1024;

    public long MaxResponseBytes { get; set; } = 4L * 1024 * 1024;

    public bool EncryptionRequired { get; set; }

    public int? EncryptionProfileId { get; set; }
    public EncryptionProfile? EncryptionProfile { get; set; }

    public bool RejectUnknownParameters { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[]? RowVersion { get; set; }

    public ICollection<ParameterRule> ParameterRules { get; set; } = new List<ParameterRule>();
    public ICollection<HeaderRule> HeaderRules { get; set; } = new List<HeaderRule>();
}
