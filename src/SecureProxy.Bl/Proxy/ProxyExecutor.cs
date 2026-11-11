using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecureProxy.Bl.Dtos;
using SecureProxy.Bl.Security;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public sealed class ProxyExecutor : IProxyExecutor
{
    public const string HttpClientName = "upstream";

    private const int DefaultTimeoutMs = 30_000;

    private readonly IProxyConfigurationProvider _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ParameterValidator _parameterValidator;
    private readonly IPayloadCryptoService _crypto;
    private readonly IUpstreamAuthProfileProvider _upstreamAuth;
    private readonly IAuditSink _audit;
    private readonly ILogger<ProxyExecutor> _logger;

    public ProxyExecutor(
        IProxyConfigurationProvider config,
        IHttpClientFactory httpClientFactory,
        ParameterValidator parameterValidator,
        IPayloadCryptoService crypto,
        IUpstreamAuthProfileProvider upstreamAuth,
        IAuditSink audit,
        ILogger<ProxyExecutor> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _parameterValidator = parameterValidator;
        _crypto = crypto;
        _upstreamAuth = upstreamAuth;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ProxyResult> ExecuteAsync(ProxyExecuteRequest request, string correlationId, IReadOnlyList<KeyValuePair<string, string>> incomingHeaders,
        string? contentType, CancellationToken callerToken, long incomingBodyBytes = -1)
    {
        var log = new RequestLog
        {
            CorrelationId = correlationId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Outcome = ExecutionOutcome.Rejected,
            ProxyStatusCode = (int)HttpStatusCode.BadRequest
        };
        var stopwatch = Stopwatch.StartNew();

        var result = await SafeRunAsync(request, incomingHeaders, contentType, log, callerToken, incomingBodyBytes);

        stopwatch.Stop();
        FinalizeAuditLog(log, result, stopwatch.ElapsedMilliseconds);

        await _audit.WriteAsync(log, CancellationToken.None);
        WriteStructuredLog(log);

        return result;
    }

    private async Task<ProxyResult> SafeRunAsync(ProxyExecuteRequest request, IReadOnlyList<KeyValuePair<string, string>> incomingHeaders,
        string? contentType, RequestLog log, CancellationToken callerToken, long incomingBodyBytes)
    {
        try
        {
            return await RunAsync(request, incomingHeaders, contentType, log, callerToken, incomingBodyBytes);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            return ProxyResult.ProxyFailure(499, ReasonCodes.CallerCancelled, "Client closed request.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled proxy error {CorrelationId}", log.CorrelationId);
            return ProxyResult.ProxyFailure(
                (int)HttpStatusCode.InternalServerError, ReasonCodes.InternalError, "Internal error.");
        }
    }

    private async Task<ProxyResult> RunAsync(ProxyExecuteRequest request, IReadOnlyList<KeyValuePair<string, string>> incomingHeaders, string? contentType,
        RequestLog log, CancellationToken callerToken, long incomingBodyBytes)
    {
        if (ValidateEnvelope(request) is { } envelopeError)
            return envelopeError;
        log.OperationKey = request.ClientId;

        if (ValidateContentType(contentType) is { } contentTypeError)
            return contentTypeError;

        var (operation, resolveError) = await ResolveOperationAsync(request.ClientId!, callerToken);
        if (resolveError is not null)
            return resolveError;
        log.DestinationId = operation!.Destination.Id;

        if (EnforceIncomingRequestSize(request, operation, incomingBodyBytes) is { } sizeError)
            return sizeError;

        var (supplied, collectError) = CollectParameters(request, operation);
        if (collectError is not null)
            return collectError;

        var validation = _parameterValidator.Validate(operation, supplied!);
        if (!validation.IsValid)
            return ProxyResult.Reject(400, validation.ReasonCode!, validation.Message!);

        var (upstreamRequest, buildError) = BuildUpstreamRequest(operation, validation.Values!, incomingHeaders);
        if (buildError is not null)
            return buildError;

        try
        {
            _upstreamAuth.Apply(upstreamRequest!, operation.Destination);
        }
        catch (UpstreamAuthProfileException)
        {
            upstreamRequest!.Dispose();
            return ProxyResult.ProxyFailure(503, ReasonCodes.UpstreamAuthUnavailable,
                "Upstream authentication is not available.");
        }

        using (upstreamRequest!)
            return await ForwardAsync(operation, upstreamRequest!, callerToken);
    }

    #region Private Methods

    private static ProxyResult? ValidateEnvelope(ProxyExecuteRequest request)
        => request is null || string.IsNullOrWhiteSpace(request.ClientId)
            ? ProxyResult.Reject(400, ReasonCodes.InvalidEnvelope, "clientId is required.")
            : null;

    private static ProxyResult? ValidateContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        var mediaType = contentType.AsSpan();
        var semicolon = mediaType.IndexOf(';');
        if (semicolon >= 0)
            mediaType = mediaType[..semicolon];

        return mediaType.Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase)
            ? null
            : ProxyResult.Reject(415, ReasonCodes.InvalidContentType, "Content-Type must be application/json.");
    }

    private static ProxyResult? EnforceIncomingRequestSize(ProxyExecuteRequest request, Operation operation, long incomingBodyBytes)
    {
        if (incomingBodyBytes >= 0)
            return incomingBodyBytes > operation.MaxRequestBytes
                ? ProxyResult.Reject(413, ReasonCodes.RequestTooLarge, "Request payload too large.")
                : null;

        long size = 0;

        if (!string.IsNullOrEmpty(request.EncryptedPayload))
            size += Encoding.UTF8.GetByteCount(request.EncryptedPayload);

        if (request.Parameters is { Count: > 0 })
            size += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(request.Parameters));

        if (!string.IsNullOrEmpty(request.UrlId))
            size += Encoding.UTF8.GetByteCount(request.UrlId);

        return size > operation.MaxRequestBytes
            ? ProxyResult.Reject(413, ReasonCodes.RequestTooLarge, "Request payload too large.")
            : null;
    }

    private async Task<(Operation? operation, ProxyResult? error)> ResolveOperationAsync(
        string operationKey, CancellationToken callerToken)
    {
        var operation = await _config.GetOperationAsync(operationKey, callerToken);
        if (operation is null)
            return (null, ProxyResult.Reject(404, ReasonCodes.OperationNotFound, "Unknown operation."));

        if (!operation.IsActive)
            return (null, ProxyResult.Reject(403, ReasonCodes.OperationDisabled, "Operation is disabled."));

        if (operation.Destination is null)
            return (null, ProxyResult.Reject(502, ReasonCodes.DestinationNotFound, "Destination not configured."));
        if (!operation.Destination.IsActive)
            return (null, ProxyResult.Reject(403, ReasonCodes.DestinationDisabled, "Destination is disabled."));

        return (operation, null);
    }

    private (Dictionary<string, JsonElement>? supplied, ProxyResult? error) CollectParameters(
        ProxyExecuteRequest request, Operation operation)
    {
        var supplied = new Dictionary<string, JsonElement>(
            request.Parameters ?? new Dictionary<string, JsonElement>(), StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(request.UrlId) && !supplied.ContainsKey("urlId"))
            supplied["urlId"] = ToJsonElement(request.UrlId);

        if (!operation.EncryptionRequired)
            return (supplied, null);

        var (plaintext, decryptError) = DecryptRequiredPayload(request, operation);
        if (decryptError is not null)
            return (null, decryptError);

        if (MergeDecryptedParameters(plaintext!, supplied) is { } mergeError)
            return (null, mergeError);

        return (supplied, null);
    }

    private (byte[]? plaintext, ProxyResult? error) DecryptRequiredPayload(
        ProxyExecuteRequest request, Operation operation)
    {
        if (string.IsNullOrWhiteSpace(request.EncryptedPayload))
            return (null, ProxyResult.Reject(400, ReasonCodes.EncryptionRequired,
                "This operation requires an encrypted payload."));

        if (operation.EncryptionProfile is null)
            return (null, ProxyResult.Reject(500, ReasonCodes.InternalError, "Internal error."));

        try
        {
            var plaintext = _crypto.Decrypt(operation.EncryptionProfile.KeyRef, request.EncryptedPayload);
            return (plaintext, null);
        }
        catch (PayloadDecryptionException)
        {
            return (null, ProxyResult.Reject(400, ReasonCodes.InvalidEncryptedPayload,
                "Encrypted payload could not be verified."));
        }
    }

    private static ProxyResult? MergeDecryptedParameters(
        byte[] plaintext, Dictionary<string, JsonElement> supplied)
    {
        if (plaintext.Length == 0)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(plaintext);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return ProxyResult.Reject(400, ReasonCodes.InvalidEncryptedPayload,
                    "Encrypted payload must be a JSON object.");

            foreach (var prop in doc.RootElement.EnumerateObject())
                supplied[prop.Name] = prop.Value.Clone();
            return null;
        }
        catch (JsonException)
        {
            return ProxyResult.Reject(400, ReasonCodes.InvalidEncryptedPayload,
                "Encrypted payload is not valid JSON.");
        }
    }

    private (HttpRequestMessage? request, ProxyResult? error) BuildUpstreamRequest(
        Operation operation,
        ValidatedParameters values,
        IReadOnlyList<KeyValuePair<string, string>> incomingHeaders)
    {
        if (!TryGetAllowedMethod(operation.HttpMethod, out var method))
            return (null, ProxyResult.Reject(500, ReasonCodes.InternalError, "Internal error."));

        var urlResult = UpstreamUrlBuilder.Build(operation.Destination, operation, values);
        if (!urlResult.IsValid)
            return (null, ProxyResult.Reject(400, urlResult.ReasonCode!, urlResult.Message!));

        var upstreamRequest = new HttpRequestMessage(method, urlResult.Uri);

        foreach (var (name, value) in HeaderPolicy.Filter(operation, incomingHeaders))
            upstreamRequest.Headers.TryAddWithoutValidation(name, value);

        if (MethodCarriesBody(method) && values.Body.Count > 0)
        {
            var bodyJson = JsonSerializer.Serialize(values.Body);
            if (Encoding.UTF8.GetByteCount(bodyJson) > operation.MaxRequestBytes)
            {
                upstreamRequest.Dispose();
                return (null, ProxyResult.Reject(413, ReasonCodes.RequestTooLarge, "Request body too large."));
            }
            upstreamRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        return (upstreamRequest, null);
    }

    private async Task<ProxyResult> ForwardAsync(
        Operation operation, HttpRequestMessage upstreamRequest, CancellationToken callerToken)
    {
        var timeoutMs = operation.Destination.TimeoutMs > 0 ? operation.Destination.TimeoutMs : DefaultTimeoutMs;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await client.SendAsync(
                upstreamRequest, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            return ProxyResult.ProxyFailure(499, ReasonCodes.CallerCancelled, "Client closed request.");
        }
        catch (OperationCanceledException)
        {
            return ProxyResult.ProxyFailure(504, ReasonCodes.UpstreamTimeout, "Upstream timed out.");
        }
        catch (HttpRequestException)
        {
            return ProxyResult.ProxyFailure(502, ReasonCodes.UpstreamConnectionFailure,
                "Upstream connection failed.");
        }

        using (upstreamResponse)
        {
            byte[]? body;
            try
            {
                body = await ReadBoundedAsync(upstreamResponse, operation.MaxResponseBytes, linkedCts.Token);
            }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
            {
                return ProxyResult.ProxyFailure(499, ReasonCodes.CallerCancelled, "Client closed request.");
            }
            catch (OperationCanceledException)
            {
                return ProxyResult.ProxyFailure(504, ReasonCodes.UpstreamTimeout, "Upstream timed out.");
            }
            catch (IOException)
            {
                return ProxyResult.ProxyFailure(502, ReasonCodes.UpstreamConnectionFailure,
                    "Upstream connection failed.");
            }

            if (body is null)
                return ProxyResult.ProxyFailure(502, ReasonCodes.ResponseTooLarge, "Upstream response too large.");

            var upstreamStatus = (int)upstreamResponse.StatusCode;
            var responseContentType =
                upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            return ProxyResult.ForwardedResult(upstreamStatus, upstreamStatus, body, responseContentType);
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpResponseMessage response, long maxBytes, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is long len && len > maxBytes)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static void FinalizeAuditLog(RequestLog log, ProxyResult result, long durationMs)
    {
        log.CompletedAtUtc = DateTimeOffset.UtcNow;
        log.DurationMs = durationMs;
        log.ForwardingOccurred = result.ForwardingOccurred;
        log.UpstreamStatusCode = result.UpstreamStatusCode;
        log.ProxyStatusCode = result.ProxyStatusCode;
        log.ReasonCode = result.ReasonCode;
        log.Outcome = ClassifyOutcome(result);
    }

    private static ExecutionOutcome ClassifyOutcome(ProxyResult result)
    {
        if (result.ForwardingOccurred)
            return ExecutionOutcome.Forwarded;

        var isProxyFault = result.ProxyStatusCode >= 500
            || result.ProxyStatusCode == 499
            || result.ProxyStatusCode == 504;
        return isProxyFault ? ExecutionOutcome.ProxyError : ExecutionOutcome.Rejected;
    }

    private void WriteStructuredLog(RequestLog log)
        => _logger.LogInformation(
            "Proxy {CorrelationId} op={OperationKey} outcome={Outcome} reason={Reason} forwarded={Forwarded} upstream={Upstream} status={Status} durationMs={Duration}",
            log.CorrelationId, log.OperationKey, log.Outcome, log.ReasonCode,
            log.ForwardingOccurred, log.UpstreamStatusCode, log.ProxyStatusCode, log.DurationMs);

    private static bool TryGetAllowedMethod(string configured, out HttpMethod method)
    {
        switch ((configured ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "GET": method = HttpMethod.Get; return true;
            case "POST": method = HttpMethod.Post; return true;
            case "PUT": method = HttpMethod.Put; return true;
            case "PATCH": method = HttpMethod.Patch; return true;
            case "DELETE": method = HttpMethod.Delete; return true;
            case "HEAD": method = HttpMethod.Head; return true;
            default: method = HttpMethod.Get; return false;
        }
    }

    private static bool MethodCarriesBody(HttpMethod method)
        => method != HttpMethod.Get && method != HttpMethod.Head;

    private static JsonElement ToJsonElement(string value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return doc.RootElement.Clone();
    }

    #endregion
}
