using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.TestSupport;

/// <summary>Canal scripté : réussit par défaut, échoue sur demande, et garde la trace de chaque envoi.</summary>
public sealed class FakeNotificationChannel : INotificationChannel
{
    private readonly HashSet<string> _rejectedContacts = new(StringComparer.Ordinal);
    private DomainError? _failure;

    public List<(ContactAddress Recipient, WakeUpMessage Message)> Sent { get; } = [];

    public int Attempts { get; private set; }

    public FakeNotificationChannel FailsWith(ErrorKind kind)
    {
        _failure = new DomainError(kind, $"Échec scripté ({kind}).");
        return this;
    }

    /// <summary>Simule la validation de format d'un vrai adaptateur pour cette coordonnée.</summary>
    public FakeNotificationChannel Rejects(string contact)
    {
        _rejectedContacts.Add(contact);
        return this;
    }

    public Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
    {
        Attempts++;

        if (_rejectedContacts.Contains(recipient.Value))
        {
            return Task.FromResult(Result.Failure<DeliveryReceipt>(ErrorKind.InvalidContact, "Coordonnée refusée (scripté)."));
        }

        if (_failure is not null)
        {
            return Task.FromResult(Result.Failure<DeliveryReceipt>(_failure));
        }

        Sent.Add((recipient, message));
        return Task.FromResult(Result.Success(new DeliveryReceipt($"fake-{Sent.Count}")));
    }
}
