using System.Globalization;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.FakeVendors.Sms;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Adapter : ramène la passerelle SMS (codes de statut) à <see cref="INotificationChannel"/>. Met
/// le message au format du canal : un seul SMS, tronqué proprement s'il est trop long.
/// </summary>
internal sealed class SmsChannelAdapter : INotificationChannel
{
    private const string Ellipsis = "…";

    private readonly ISmsGatewayClient _gateway;

    public SmsChannelAdapter(ISmsGatewayClient gateway) => _gateway = gateway;

    public async Task<Result<DeliveryReceipt>> SendAsync(ContactAddress recipient, WakeUpMessage message, CancellationToken cancellationToken)
    {
        var response = await _gateway
            .SubmitAsync(recipient.Value, FitInOneSms(message.Body), cancellationToken)
            .ConfigureAwait(false);

        return response.StatusCode switch
        {
            SmsStatusCodes.Accepted when response.MessageId is not null => Result.Success(new DeliveryReceipt(response.MessageId)),
            // Sans le numéro : ce message part dans les journaux, l'alerte et la réponse HTTP.
            SmsStatusCodes.InvalidNumber => Result.Failure<DeliveryReceipt>(
                ErrorKind.InvalidContact, "Numéro refusé par la passerelle SMS."),
            _ => Result.Failure<DeliveryReceipt>(
                ErrorKind.ChannelUnavailable,
                $"Passerelle SMS : statut {response.StatusCode.ToString(CultureInfo.InvariantCulture)}."),
        };
    }

    private static string FitInOneSms(string body)
    {
        if (body.Length <= SmsGatewayClient.MaxLength)
        {
            return body;
        }

        var cut = SmsGatewayClient.MaxLength - Ellipsis.Length;

        // Jamais au milieu d'une paire de substitution (un emoji dans un titre) : l'UTF-16 serait
        // invalide, et une vraie passerelle refuserait le message.
        if (char.IsHighSurrogate(body[cut - 1]))
        {
            cut--;
        }

        return string.Concat(body.AsSpan(0, cut), Ellipsis);
    }
}
