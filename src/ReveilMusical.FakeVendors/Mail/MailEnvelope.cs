namespace ReveilMusical.FakeVendors.Mail;

public sealed record MailEnvelope(string From, string To, string Subject, string HtmlBody);
