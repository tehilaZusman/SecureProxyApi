namespace SecureProxy.Dal.Domain;

public enum ParameterLocation
{
    Path = 0,
    Query = 1,
    Body = 2
}

public enum ParameterDataType
{
    String = 0,
    Integer = 1,
    Boolean = 2,
    Guid = 3
}

public class ParameterRule
{
    public int Id { get; set; }

    public int OperationId { get; set; }
    public Operation Operation { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public bool Required { get; set; }

    public ParameterDataType DataType { get; set; } = ParameterDataType.String;

    public ParameterLocation Location { get; set; } = ParameterLocation.Query;

    public int? MaxLength { get; set; }

    public long? MinValue { get; set; }

    public long? MaxValue { get; set; }

    public string? Pattern { get; set; }
}
