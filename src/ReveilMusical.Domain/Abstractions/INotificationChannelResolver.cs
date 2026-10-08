using ReveilMusical.Domain.Model;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>Retrouve le canal enregistré sous un identifiant, ou <c>null</c> s'il n'existe pas.</summary>
public interface INotificationChannelResolver
{
    public INotificationChannel? Resolve(ChannelId channel);
}
