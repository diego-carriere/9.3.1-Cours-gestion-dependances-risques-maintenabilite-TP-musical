using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Model;
using ReveilMusical.Infrastructure.Notifications;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class LoggingOperatorAlerterTests
{
    [Fact]
    public async Task An_alert_is_a_critical_log_naming_the_user_and_the_cause()
    {
        var logger = new CapturingLogger<LoggingOperatorAlerter>();

        await new LoggingOperatorAlerter(logger).RaiseAsync(
            new OperatorAlert(UserId.Create("42").Value, "sms (Failed)"), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Contains("42", entry.Message, StringComparison.Ordinal);
        Assert.Contains("sms (Failed)", entry.Message, StringComparison.Ordinal);
    }
}
