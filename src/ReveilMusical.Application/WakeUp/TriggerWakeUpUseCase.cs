using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Application.WakeUp;

/// <summary>
/// Facade du réveil : profil, puis morceau, puis notification. Logique pure : toute E/S passe par
/// les ports injectés, aucune connaissance d'un fournisseur, d'un canal ou de HTTP.
/// </summary>
public sealed class TriggerWakeUpUseCase : ITriggerWakeUpUseCase
{
    private readonly IUserProfileProvider _profiles;
    private readonly TrackSelector _trackSelector;
    private readonly NotificationDispatcher _dispatcher;
    private readonly IClock _clock;

    public TriggerWakeUpUseCase(
        IUserProfileProvider profiles,
        TrackSelector trackSelector,
        NotificationDispatcher dispatcher,
        IClock clock)
    {
        _profiles = profiles;
        _trackSelector = trackSelector;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<Result<WakeUpReport>> ExecuteAsync(WakeUpRequest request, CancellationToken cancellationToken)
    {
        var triggeredAt = _clock.UtcNow;

        var profile = await _profiles.GetAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (profile.IsFailure)
        {
            return Result.Failure<WakeUpReport>(profile.Error);
        }

        var choice = await _trackSelector
            .SelectAsync(profile.Value, request.Day, request.Weather, cancellationToken)
            .ConfigureAwait(false);

        var message = WakeUpMessage.Compose(profile.Value.DisplayName, choice.Track, request.Day, request.Weather);

        var outcome = await _dispatcher.DispatchAsync(profile.Value, message, cancellationToken).ConfigureAwait(false);

        return Result.Success(new WakeUpReport(profile.Value.Id, profile.Value.PreferredChannel, choice, outcome, triggeredAt));
    }
}
