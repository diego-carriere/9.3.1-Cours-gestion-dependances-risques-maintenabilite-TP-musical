using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Infrastructure.Notifications;

namespace ReveilMusical.Infrastructure;

/// <summary>
/// Point d'entrée unique de la couche Infrastructure dans le composition root. Tous les adaptateurs
/// qu'elle enregistre sont <c>internal</c> : l'hôte ne peut pas en instancier un lui-même.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotifications(configuration);

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
