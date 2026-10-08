using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.TestSupport;

public sealed class RecordingOperatorAlerter : IOperatorAlerter
{
    public List<OperatorAlert> Alerts { get; } = [];

    public Task RaiseAsync(OperatorAlert alert, CancellationToken cancellationToken)
    {
        Alerts.Add(alert);
        return Task.CompletedTask;
    }
}
