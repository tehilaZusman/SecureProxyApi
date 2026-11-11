namespace SecureProxy.Api.Dtos;

public class ProxyErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public ProxyErrorResponse() { }

    public ProxyErrorResponse(string code, string error, string correlationId)
    {
        Code = code;
        Error = error;
        CorrelationId = correlationId;
    }
}
