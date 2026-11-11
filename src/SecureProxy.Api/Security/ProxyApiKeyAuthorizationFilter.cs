using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SecureProxy.Api.Dtos;

namespace SecureProxy.Api.Security;

public sealed class ProxyApiKeyAuthorizationFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Proxy-Api-Key";
    public const string ConfigKey = "Proxy:ApiKey";

    private readonly string? _expectedKey;
    private readonly ILogger<ProxyApiKeyAuthorizationFilter> _logger;

    public ProxyApiKeyAuthorizationFilter(
        IConfiguration configuration,
        ILogger<ProxyApiKeyAuthorizationFilter> logger)
    {
        _expectedKey = configuration[ConfigKey];
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var cid)
            && cid is string value ? value : string.Empty;

        if (string.IsNullOrWhiteSpace(_expectedKey))
        {
            _logger.LogError("Proxy execution blocked because its API key is not configured. CorrelationId={CorrelationId}", correlationId);
            context.Result = Error(503, "proxy_not_configured", "Proxy authentication is not configured.", correlationId);
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var supplied)
            || string.IsNullOrWhiteSpace(supplied))
        {
            _logger.LogWarning("Proxy authentication rejected: missing key. CorrelationId={CorrelationId}", correlationId);
            context.Result = Error(401, "unauthorized", "Missing proxy API key.", correlationId);
            return;
        }

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(_expectedKey);
        var suppliedBytes = System.Text.Encoding.UTF8.GetBytes(supplied.ToString());
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
        {
            _logger.LogWarning("Proxy authentication rejected: invalid key. CorrelationId={CorrelationId}", correlationId);
            context.Result = Error(403, "forbidden", "Invalid proxy API key.", correlationId);
        }
    }

    private static ObjectResult Error(int status, string code, string message, string correlationId)
        => new(new ProxyErrorResponse(code, message, correlationId)) { StatusCode = status };
}
