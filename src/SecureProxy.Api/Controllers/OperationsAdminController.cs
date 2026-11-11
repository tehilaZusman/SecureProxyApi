using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureProxy.Api.Dtos;
using SecureProxy.Api.Security;
using SecureProxy.Bl.Proxy;
using SecureProxy.Dal.Domain;
using SecureProxy.Dal.Repositories;

namespace SecureProxy.Api.Controllers;

[ApiController]
[Route("api/admin/operations")]
[ServiceFilter(typeof(AdminApiKeyAuthorizationFilter))]
public sealed class OperationsAdminController : ControllerBase
{
    private readonly IOperationRepository _operations;
    private readonly IConfigurationChangeLogRepository _changeLog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProxyConfigurationProvider _configProvider;

    public OperationsAdminController(
        IOperationRepository operations,
        IConfigurationChangeLogRepository changeLog,
        IUnitOfWork unitOfWork,
        IProxyConfigurationProvider configProvider)
    {
        _operations = operations;
        _changeLog = changeLog;
        _unitOfWork = unitOfWork;
        _configProvider = configProvider;
    }

    public sealed class SetEnabledRequest
    {
        public bool IsActive { get; set; }
        public string? RowVersion { get; set; }
    }

    [HttpPatch("{key}/enabled")]
    public async Task<IActionResult> SetEnabled(
        string key, [FromBody] SetEnabledRequest body, CancellationToken ct)
    {
        var op = await _operations.GetTrackedByKeyAsync(key, ct);
        if (op is null)
            return NotFound(new ProxyErrorResponse(ReasonCodes.OperationNotFound, "Unknown operation.", ""));

        if (!string.IsNullOrEmpty(body.RowVersion))
        {
            try
            {
                var expected = Convert.FromBase64String(body.RowVersion);
                _operations.SetExpectedRowVersion(op, expected);
            }
            catch (FormatException)
            {
                return BadRequest(new ProxyErrorResponse(
                    ReasonCodes.InvalidParameters, "Invalid rowVersion.", ""));
            }
        }

        var before = op.IsActive;
        op.IsActive = body.IsActive;
        op.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _changeLog.Add(new ConfigurationChangeLog
        {
            EntityType = nameof(Operation),
            EntityId = op.Id.ToString(),
            Action = "Updated",
            Summary = $"IsActive: {before} -> {op.IsActive}",
            ChangedBy = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name
                : "admin-api-key",
            ChangedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProxyErrorResponse(
                ReasonCodes.ConfigurationConflict,
                "The operation was modified by someone else. Reload and retry.", ""));
        }

        _configProvider.Invalidate(key);

        return Ok(new
        {
            op.OperationKey,
            op.IsActive,
            RowVersion = op.RowVersion is null ? null : Convert.ToBase64String(op.RowVersion)
        });
    }
}
