using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Canal scripté : réussit par défaut, échoue ou lève sur demande, et garde la trace de chaque envoi.
/// </summary>
public sealed class FakeNotificationChannel : INotificationChannel
{
    private readonly HashSet<string> _rejectedContacts = new(StringComparer.Ordinal);
    private DomainError? _failure;
    private Exception? _exception;

    public List<(ContactAddress Recipient, WakeUpMessage Message)> Sent { get; } = [];

    public int Attempts { get; private set; }

    public FakeNotificationChannel FailsWith(ErrorKind kind)
    {
        _failure = new DomainError(kind, $"Échec scripté ({kind}).");
        return this;
    }

    /// <summary>Simule un adaptateur qui viole son contrat : il lève au lieu de renvoyer un échec.</summary>
    public FakeNotificationChannel Throws(Exception exception)
    {
        _exception = exception;
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
        cancellationToken.ThrowIfCancellationRequested();

        if (_exception is not null)
        {
            throw _exception;
        }

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
