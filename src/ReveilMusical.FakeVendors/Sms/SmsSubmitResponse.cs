namespace ReveilMusical.FakeVendors.Sms;

/// <summary>Le style de ce SDK : jamais d'exception, un code de statut à interpréter.</summary>
public sealed record SmsSubmitResponse(int StatusCode, string? MessageId);

public static class SmsStatusCodes
{
    public const int Accepted = 0;
    public const int InvalidNumber = 21;
    public const int MessageTooLong = 30;
    public const int ServiceUnavailable = 503;
}
