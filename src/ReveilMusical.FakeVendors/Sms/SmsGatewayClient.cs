namespace ReveilMusical.FakeVendors.Sms;

/// <summary>SDK SMS simulé : asynchrone, ne lève jamais, répond par un code de statut.</summary>
public sealed class SmsGatewayClient : ISmsGatewayClient
{
    /// <summary>Un seul SMS : au-delà, la passerelle refuse (pas de concaténation).</summary>
    public const int MaxLength = 160;

    private readonly SmsGatewaySettings _settings;
    private readonly OutboxFile _outbox;

    public SmsGatewayClient(SmsGatewaySettings settings)
    {
        _settings = settings;
        _outbox = new OutboxFile(settings.OutboxDirectory, "sms.log", settings.WriteToConsole);
    }

    public async Task<SmsSubmitResponse> SubmitAsync(string msisdn, string text, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (_settings.SimulateOutage)
        {
            return new SmsSubmitResponse(SmsStatusCodes.ServiceUnavailable, null);
        }

        if (!IsE164(msisdn))
        {
            return new SmsSubmitResponse(SmsStatusCodes.InvalidNumber, null);
        }

        if (text.Length > MaxLength)
        {
            return new SmsSubmitResponse(SmsStatusCodes.MessageTooLong, null);
        }

        var messageId = "sms-" + Guid.NewGuid().ToString("N");
        _outbox.Append($"[SMS {messageId}] from={_settings.SenderName} to={msisdn} text=\"{text}\"");
        return new SmsSubmitResponse(SmsStatusCodes.Accepted, messageId);
    }

    private static bool IsE164(string value) =>
        value.Length is >= 9 and <= 16 && value[0] == '+' && value.Skip(1).All(char.IsAsciiDigit);
}
