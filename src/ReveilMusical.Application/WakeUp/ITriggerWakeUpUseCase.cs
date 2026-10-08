using ReveilMusical.Domain.Results;

namespace ReveilMusical.Application.WakeUp;

public interface ITriggerWakeUpUseCase
{
    /// <summary>
    /// Échoue seulement si l'utilisateur est introuvable ou le service utilisateur en panne. Un
    /// réveil non remis reste un succès dont le rapport dit <see cref="WakeUpReport.Delivered"/> faux.
    /// </summary>
    public Task<Result<WakeUpReport>> ExecuteAsync(WakeUpRequest request, CancellationToken cancellationToken);
}
