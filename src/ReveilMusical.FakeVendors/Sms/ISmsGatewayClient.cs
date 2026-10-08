namespace ReveilMusical.FakeVendors.Sms;

public interface ISmsGatewayClient
{
    /// <summary>Envoie un SMS à un numéro au format E.164 (« +33612345678 »).</summary>
    public Task<SmsSubmitResponse> SubmitAsync(string msisdn, string text, CancellationToken cancellationToken = default);
}
