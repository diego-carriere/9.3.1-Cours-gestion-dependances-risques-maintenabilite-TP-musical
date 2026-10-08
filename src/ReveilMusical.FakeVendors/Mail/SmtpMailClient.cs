namespace ReveilMusical.FakeVendors.Mail;

/// <summary>
/// SDK mail simulé, à l'ancienne : une classe concrète sans interface, un appel bloquant, et une
/// exception pour tout échec. C'est à l'adaptateur de l'Infrastructure de traduire cela.
/// </summary>
public sealed class SmtpMailClient
{
    private readonly SmtpMailSettings _settings;
    private readonly OutboxFile _outbox;

    public SmtpMailClient(SmtpMailSettings settings)
    {
        _settings = settings;
        _outbox = new OutboxFile(settings.OutboxDirectory, "mail.log", settings.WriteToConsole);
    }

    public SmtpMailSettings Settings => _settings;

    /// <returns>L'identifiant du message accepté par le serveur.</returns>
    /// <exception cref="MailDeliveryException">Serveur injoignable ou destinataire invalide.</exception>
    public string Send(MailEnvelope envelope)
    {
        if (_settings.SimulateOutage)
        {
            throw new MailDeliveryException(MailDeliveryFailure.ServerUnavailable, "SMTP server unreachable (simulated).");
        }

        if (!IsAddress(envelope.To))
        {
            throw new MailDeliveryException(MailDeliveryFailure.InvalidRecipient, $"Invalid recipient '{envelope.To}'.");
        }

        var messageId = "mail-" + Guid.NewGuid().ToString("N");
        _outbox.Append($"[MAIL {messageId}] from={envelope.From} to={envelope.To} subject=\"{envelope.Subject}\" body={envelope.HtmlBody}");
        return messageId;
    }

    private static bool IsAddress(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0;
    }
}
