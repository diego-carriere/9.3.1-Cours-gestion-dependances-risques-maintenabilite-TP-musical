using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Alerte opérateur par un log <see cref="LogLevel.Critical"/>, que la supervision remonte. Un
/// pager ou un ticket serait un autre <see cref="IOperatorAlerter"/>, sans rien changer ailleurs.
/// </summary>
internal sealed partial class LoggingOperatorAlerter : IOperatorAlerter
{
    private readonly ILogger<LoggingOperatorAlerter> _logger;

    public LoggingOperatorAlerter(ILogger<LoggingOperatorAlerter> logger) => _logger = logger;

    public Task RaiseAsync(OperatorAlert alert, CancellationToken cancellationToken)
    {
        LogAlert(alert.UserId.Value, alert.Summary);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "WAKE-UP NOT DELIVERED for user {UserId}: {Summary}")]
    private partial void LogAlert(string userId, string summary);
}
