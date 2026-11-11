using Microsoft.AspNetCore.Mvc;
using SecureProxy.Api.Dtos;
using SecureProxy.Api.Middleware;
using SecureProxy.Api.Security;
using SecureProxy.Bl.Dtos;
using SecureProxy.Bl.Proxy;

namespace SecureProxy.Api.Controllers;

[ApiController]
[Route("api/proxy")]
[ServiceFilter(typeof(ProxyApiKeyAuthorizationFilter))]
public sealed class ProxyController : ControllerBase
{
    private readonly IProxyExecutor _executor;

    public ProxyController(IProxyExecutor executor)
    {
        _executor = executor;
    }

    [HttpPost("execute")]
    [Consumes("application/json")]
    public async Task<IActionResult> Execute([FromBody] ProxyExecuteRequest request,CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();

        var incomingHeaders = Request.Headers
            .Select(h => new KeyValuePair<string, string>(h.Key, h.Value.ToString()))
            .ToList();

        var result = await _executor.ExecuteAsync(
            request,
            correlationId,
            incomingHeaders,
            Request.ContentType,
            cancellationToken,
            Request.ContentLength ?? RequestBodySizeTrackingMiddleware.GetBytesRead(HttpContext));

        if (result.ForwardingOccurred)
        {
            Response.StatusCode = result.ProxyStatusCode;
            Response.ContentType = result.ResponseContentType ?? "application/octet-stream";
            var body = result.ResponseBody ?? Array.Empty<byte>();
            if (body.Length > 0)
                await Response.Body.WriteAsync(body, cancellationToken);
            return new EmptyResult();
        }

        var error = new ProxyErrorResponse(result.ReasonCode, result.Message, correlationId);
        return StatusCode(result.ProxyStatusCode, error);
    }
}
