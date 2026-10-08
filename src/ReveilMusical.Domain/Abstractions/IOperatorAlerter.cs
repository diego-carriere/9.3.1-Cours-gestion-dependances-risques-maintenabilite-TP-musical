using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Abstractions;

public interface IOperatorAlerter
{
    public Task RaiseAsync(OperatorAlert alert, CancellationToken cancellationToken);
}
