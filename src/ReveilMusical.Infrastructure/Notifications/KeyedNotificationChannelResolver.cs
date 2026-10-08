using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Factory branchée sur la DI : retrouve le canal enregistré sous la clé d'un
/// <see cref="ChannelId"/> (services à clés). Ajouter un canal n'ajoute rien ici.
/// </summary>
internal sealed class KeyedNotificationChannelResolver : INotificationChannelResolver
{
    private readonly IServiceProvider _serviceProvider;

    public KeyedNotificationChannelResolver(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public INotificationChannel? Resolve(ChannelId channel) =>
        _serviceProvider.GetKeyedService<INotificationChannel>(channel.Value);
}
