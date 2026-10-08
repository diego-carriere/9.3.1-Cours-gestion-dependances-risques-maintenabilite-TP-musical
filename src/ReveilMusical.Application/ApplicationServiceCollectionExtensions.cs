using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Application.Options;
using ReveilMusical.Application.WakeUp;

namespace ReveilMusical.Application;

/// <summary>
/// Point d'entrée de la couche Application dans le composition root. N'enregistre que ce que cette
/// couche possède ; les ports qu'elle consomme sont enregistrés par AddInfrastructure. La liaison à
/// la configuration est passée par l'hôte (paramètre <c>configure</c>) : l'Application ne dépend
/// pas de Microsoft.Extensions.Configuration.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, Action<WakeUpOptions>? configure = null)
    {
        services.AddOptions<WakeUpOptions>()
            .Configure(configure ?? (static _ => { }))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WakeUpOptions>, WakeUpOptionsValidator>());

        // Scoped : une unité de travail par requête, sans état entre deux réveils. Aucun singleton
        // ici, donc aucun risque de dépendance captive sur les ports transients qu'ils consomment.
        services.TryAddScoped<TrackSelector>();
        services.TryAddScoped<NotificationDispatcher>();
        services.TryAddScoped<ITriggerWakeUpUseCase, TriggerWakeUpUseCase>();

        return services;
    }
}
