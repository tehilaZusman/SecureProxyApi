using Microsoft.Extensions.Logging;
using SecureProxy.Dal.Domain;
using SecureProxy.Dal.Repositories;

namespace SecureProxy.Bl.Proxy;

public sealed class DatabaseAuditSink : IAuditSink
{
    private readonly IRequestLogRepository _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DatabaseAuditSink> _logger;

    public DatabaseAuditSink(
        IRequestLogRepository logs, IUnitOfWork unitOfWork, ILogger<DatabaseAuditSink> logger)
    {
        _logs = logs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task WriteAsync(RequestLog log, CancellationToken ct)
    {
        try
        {
            _logs.Add(log);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist audit record {CorrelationId} outcome={Outcome} reason={Reason}",
                log.CorrelationId, log.Outcome, log.ReasonCode);
        }
    }
}
