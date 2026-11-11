using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public sealed class ValidatedParameters
{
    public Dictionary<string, string> Path { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Query { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Body { get; } = new(StringComparer.Ordinal);
}

public sealed class ParameterValidationResult
{
    public bool IsValid { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }
    public ValidatedParameters? Values { get; init; }

    public static ParameterValidationResult Fail(string code, string message)
        => new() { IsValid = false, ReasonCode = code, Message = message };

    public static ParameterValidationResult Ok(ValidatedParameters values)
        => new() { IsValid = true, Values = values };
}

public sealed class ParameterValidator
{
    private static readonly Regex TraversalPattern =
        new(@"(\.\.)|[/\\]|%2e%2e|%2f|%5c|://", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    public ParameterValidationResult Validate(
        Operation operation,
        IReadOnlyDictionary<string, JsonElement> supplied)
    {
        if (CheckUnknownParameters(operation, supplied) is { } unknownError)
            return unknownError;

        var values = new ValidatedParameters();

        foreach (var rule in operation.ParameterRules)
        {
            if (ApplyRule(rule, supplied, values) is { } ruleError)
                return ruleError;
        }

        return ParameterValidationResult.Ok(values);
    }

    #region Private Methods

    private static ParameterValidationResult? CheckUnknownParameters(
        Operation operation, IReadOnlyDictionary<string, JsonElement> supplied)
    {
        if (!operation.RejectUnknownParameters)
            return null;

        var known = operation.ParameterRules.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in supplied.Keys)
        {
            if (!known.Contains(name))
                return ParameterValidationResult.Fail(
                    ReasonCodes.UnknownParameter, $"Unknown parameter: {name}");
        }
        return null;
    }

    private static ParameterValidationResult? ApplyRule(
        ParameterRule rule,
        IReadOnlyDictionary<string, JsonElement> supplied,
        ValidatedParameters values)
    {
        if (!IsPresent(rule, supplied, out var element))
        {
            return rule.Required
                ? ParameterValidationResult.Fail(
                    ReasonCodes.MissingParameter, $"Missing required parameter: {rule.Name}")
                : null;
        }

        var (ok, stringValue, error) = CoerceAndValidate(rule, element);
        if (!ok)
            return ParameterValidationResult.Fail(ReasonCodes.InvalidParameters, error!);

        return PlaceValue(rule, stringValue!, values);
    }

    private static bool IsPresent(
        ParameterRule rule, IReadOnlyDictionary<string, JsonElement> supplied, out JsonElement element)
    {
        return supplied.TryGetValue(rule.Name, out element)
            && element.ValueKind != JsonValueKind.Null
            && element.ValueKind != JsonValueKind.Undefined;
    }

    private static ParameterValidationResult? PlaceValue(
        ParameterRule rule, string value, ValidatedParameters values)
    {
        switch (rule.Location)
        {
            case ParameterLocation.Path:
                if (TraversalPattern.IsMatch(value))
                    return ParameterValidationResult.Fail(
                        ReasonCodes.PathTraversal, $"Illegal characters in path parameter: {rule.Name}");
                values.Path[rule.Name] = value;
                return null;

            case ParameterLocation.Query:
                values.Query[rule.Name] = value;
                return null;

            default:
                values.Body[rule.Name] = value;
                return null;
        }
    }

    private static (bool ok, string? value, string? error) CoerceAndValidate(
        ParameterRule rule, JsonElement element)
        => rule.DataType switch
        {
            ParameterDataType.Integer => ValidateInteger(rule, element),
            ParameterDataType.Boolean => ValidateBoolean(rule, element),
            ParameterDataType.Guid => ValidateGuid(rule, element),
            _ => ValidateString(rule, element)
        };

    private static (bool, string?, string?) ValidateInteger(ParameterRule rule, JsonElement element)
    {
        long parsed;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out parsed)) { }
        else if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out parsed)) { }
        else
            return (false, null, $"Parameter {rule.Name} must be an integer.");

        if (rule.MinValue.HasValue && parsed < rule.MinValue.Value)
            return (false, null, $"Parameter {rule.Name} below minimum.");
        if (rule.MaxValue.HasValue && parsed > rule.MaxValue.Value)
            return (false, null, $"Parameter {rule.Name} above maximum.");

        return (true, parsed.ToString(CultureInfo.InvariantCulture), null);
    }

    private static (bool, string?, string?) ValidateBoolean(ParameterRule rule, JsonElement element)
    {
        bool parsed;
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            parsed = element.GetBoolean();
        else if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out parsed)) { }
        else
            return (false, null, $"Parameter {rule.Name} must be a boolean.");

        return (true, parsed ? "true" : "false", null);
    }

    private static (bool, string?, string?) ValidateGuid(ParameterRule rule, JsonElement element)
    {
        var s = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        if (s is null || !Guid.TryParse(s, out var g))
            return (false, null, $"Parameter {rule.Name} must be a GUID.");

        return (true, g.ToString(), null);
    }

    private static (bool, string?, string?) ValidateString(ParameterRule rule, JsonElement element)
    {
        var s = (element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText())
                ?? string.Empty;

        if (rule.MaxLength.HasValue && s.Length > rule.MaxLength.Value)
            return (false, null, $"Parameter {rule.Name} exceeds maximum length.");

        if (!string.IsNullOrEmpty(rule.Pattern)
            && !Regex.IsMatch(s, rule.Pattern, RegexOptions.None, PatternTimeout))
            return (false, null, $"Parameter {rule.Name} does not match the required format.");

        return (true, s, null);
    }

    #endregion
}
