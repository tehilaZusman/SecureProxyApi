using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SecureProxy.Api.Dtos;
using SecureProxy.Bl.Proxy;

namespace SecureProxy.Api.Security;

public sealed class AdminApiKeyAuthorizationFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Api-Key";
    public const string ConfigKey = "Admin:ApiKey";

    private readonly string? _expectedKey;

    public AdminApiKeyAuthorizationFilter(IConfiguration configuration)
    {
        _expectedKey = configuration[ConfigKey];
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var cid)
            && cid is string s ? s : string.Empty;

        if (string.IsNullOrEmpty(_expectedKey))
        {
            context.Result = Error(503, ReasonCodes.InternalError,
                "Admin interface is not configured.", correlationId);
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            context.Result = Error(401, "unauthorized", "Missing admin API key.", correlationId);
            return;
        }

        if (!FixedTimeEquals(provided.ToString(), _expectedKey))
        {
            context.Result = Error(403, "forbidden", "Invalid admin API key.", correlationId);
            return;
        }
    }

    private static ObjectResult Error(int status, string code, string message, string correlationId)
        => new(new ProxyErrorResponse(code, message, correlationId)) { StatusCode = status };

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
