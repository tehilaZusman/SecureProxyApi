namespace SecureProxy.Dal.Domain;

public class HeaderRule
{
    public int Id { get; set; }

    public int OperationId { get; set; }
    public Operation Operation { get; set; } = null!;

    public string HeaderName { get; set; } = string.Empty;
}
