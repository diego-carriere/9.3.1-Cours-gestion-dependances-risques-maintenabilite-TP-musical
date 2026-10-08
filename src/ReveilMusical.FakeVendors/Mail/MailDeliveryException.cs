namespace ReveilMusical.FakeVendors.Mail;

public enum MailDeliveryFailure
{
    ServerUnavailable,
    InvalidRecipient,
}

/// <summary>Le style de ce SDK : synchrone, et tout échec est une exception.</summary>
public sealed class MailDeliveryException : Exception
{
    public MailDeliveryException()
    {
    }

    public MailDeliveryException(string message)
        : base(message)
    {
    }

    public MailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public MailDeliveryException(MailDeliveryFailure failure, string message)
        : base(message) => Failure = failure;

    public MailDeliveryFailure Failure { get; }
}
