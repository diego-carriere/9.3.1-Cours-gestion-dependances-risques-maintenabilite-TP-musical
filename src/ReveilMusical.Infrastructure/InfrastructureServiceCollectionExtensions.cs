using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Infrastructure.Time;
using ReveilMusical.Infrastructure.Users;

namespace ReveilMusical.Infrastructure;

/// <summary>
/// Point d'entrée unique de la couche Infrastructure dans le composition root. Tous les adaptateurs
/// qu'elle enregistre sont <c>internal</c> : l'hôte ne peut pas en instancier un lui-même.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddUserDirectory(configuration);
        services.AddMusic(configuration);
        services.AddNotifications(configuration);

        // Singletons sans état ni dépendance : aucun risque de dépendance captive.
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IRandom, SystemRandom>();

        return services;
    }

    /// <summary>
    /// Ajouter un canal (WhatsApp, appel vocal...) : un adaptateur <see cref="INotificationChannel"/>
    /// et cet appel. Ni le Domaine ni l'Application ne changent ; le décorateur de résilience est
    /// posé automatiquement.
    /// </summary>
    public static IServiceCollection AddNotificationChannel<TAdapter>(this IServiceCollection services, string channelId)
        where TAdapter : class, INotificationChannel =>
        services.AddChannel<TAdapter>(channelId);
}
