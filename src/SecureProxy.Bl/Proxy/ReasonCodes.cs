namespace SecureProxy.Bl.Proxy;

public static class ReasonCodes
{
    public const string InvalidEnvelope = "invalid_envelope";
    public const string OperationNotFound = "operation_not_found";
    public const string OperationDisabled = "operation_disabled";
    public const string DestinationNotFound = "destination_not_found";
    public const string DestinationDisabled = "destination_disabled";
    public const string InvalidParameters = "invalid_parameters";
    public const string UnknownParameter = "unknown_parameter";
    public const string MissingParameter = "missing_parameter";
    public const string PathTraversal = "path_traversal";
    public const string InvalidContentType = "invalid_content_type";
    public const string RequestTooLarge = "request_too_large";
    public const string ResponseTooLarge = "response_too_large";
    public const string EncryptionRequired = "encryption_required";
    public const string InvalidEncryptedPayload = "invalid_encrypted_payload";
    public const string UpstreamTimeout = "upstream_timeout";
    public const string UpstreamConnectionFailure = "upstream_connection_failure";
    public const string UpstreamAuthUnavailable = "upstream_auth_unavailable";
    public const string CallerCancelled = "caller_cancelled";
    public const string ConfigurationConflict = "configuration_conflict";
    public const string InternalError = "internal_error";
    public const string Forwarded = "forwarded";
}
