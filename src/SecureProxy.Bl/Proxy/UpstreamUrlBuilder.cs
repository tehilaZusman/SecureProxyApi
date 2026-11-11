using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public sealed class UrlBuildResult
{
    public bool IsValid { get; init; }
    public Uri? Uri { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }

    public static UrlBuildResult Ok(Uri uri) => new() { IsValid = true, Uri = uri };
    public static UrlBuildResult Fail(string code, string message)
        => new() { IsValid = false, ReasonCode = code, Message = message };
}

public static class UpstreamUrlBuilder
{
    public static UrlBuildResult Build(
        Destination destination,
        Operation operation,
        ValidatedParameters parameters)
    {
        if (!TryParseBaseUri(destination.BaseUrl, out var baseUri))
            return UrlBuildResult.Fail(ReasonCodes.DestinationNotFound, "Destination is misconfigured.");

        var (relativePath, pathError) = ExpandPathTemplate(operation.RelativePathTemplate, parameters);
        if (pathError is not null)
            return pathError;

        var (finalUri, buildError) = ComposeUri(baseUri!, relativePath!, parameters.Query);
        if (buildError is not null)
            return buildError;

        return ValidateFinalUri(destination, baseUri!, finalUri!)
            ?? UrlBuildResult.Ok(finalUri!);
    }

    #region Private Methods

    private static bool TryParseBaseUri(string baseUrl, out Uri? baseUri)
    {
        baseUri = null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme is not ("http" or "https"))
            return false;
        if (!string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
            return false;
        baseUri = parsed;
        return true;
    }

    private static (string? path, UrlBuildResult? error) ExpandPathTemplate(
        string template, ValidatedParameters parameters)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c != '{')
            {
                sb.Append(c);
                i++;
                continue;
            }

            var end = template.IndexOf('}', i + 1);
            if (end < 0)
                return (null, UrlBuildResult.Fail(ReasonCodes.InternalError, "Destination is misconfigured."));

            var name = template.Substring(i + 1, end - i - 1);
            if (!parameters.Path.TryGetValue(name, out var value))
                return (null, UrlBuildResult.Fail(ReasonCodes.MissingParameter, $"Missing path parameter: {name}"));

            sb.Append(Uri.EscapeDataString(value));
            i = end + 1;
        }

        var relativePath = sb.ToString();
        if (!relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;

        return (relativePath, null);
    }

    private static (Uri? uri, UrlBuildResult? error) ComposeUri(
        Uri baseUri, string relativePath, IReadOnlyDictionary<string, string> query)
    {
        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        var builder = new UriBuilder(baseUri)
        {
            Path = basePath + relativePath,
            Query = BuildQuery(query)
        };

        try
        {
            return (builder.Uri, null);
        }
        catch (UriFormatException)
        {
            return (null, UrlBuildResult.Fail(ReasonCodes.InvalidParameters, "Invalid request."));
        }
    }

    private static UrlBuildResult? ValidateFinalUri(Destination destination, Uri baseUri, Uri finalUri)
    {
        if (!string.Equals(finalUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(finalUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase))
            return UrlBuildResult.Fail(ReasonCodes.PathTraversal, "Request violates destination policy.");

        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        if (basePath.Length > 0 && !IsUnderBasePath(finalUri.AbsolutePath, basePath))
            return UrlBuildResult.Fail(ReasonCodes.PathTraversal, "Request violates destination policy.");

        if (!IsPortAllowed(destination, finalUri))
            return UrlBuildResult.Fail(ReasonCodes.DestinationDisabled, "Request violates destination policy.");

        return null;
    }

    private static bool IsUnderBasePath(string absolutePath, string basePath)
    {
        if (absolutePath.Equals(basePath, StringComparison.Ordinal))
            return true;

        return absolutePath.Length > basePath.Length
            && absolutePath[basePath.Length] == '/'
            && absolutePath.StartsWith(basePath, StringComparison.Ordinal);
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string> query)
    {
        if (query.Count == 0)
            return string.Empty;

        return string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static bool IsPortAllowed(Destination destination, Uri uri)
    {
        if (string.IsNullOrWhiteSpace(destination.AllowedPorts))
        {
            var defaultPort = uri.Scheme == "https" ? 443 : 80;
            return uri.Port == defaultPort;
        }

        var allowed = destination.AllowedPorts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var n) ? n : -1)
            .Where(n => n > 0)
            .ToHashSet();

        return allowed.Contains(uri.Port);
    }

    #endregion
}
