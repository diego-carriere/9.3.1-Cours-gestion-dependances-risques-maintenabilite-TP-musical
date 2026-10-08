using ReveilMusical.Domain.Results;

namespace ReveilMusical.Api.Errors;

/// <summary>
/// Le seul endroit qui traduit un <see cref="ErrorKind"/> en code HTTP. Exhaustivité vérifiée par
/// test (un switch sur une enum ne l'est pas pour le compilateur).
/// </summary>
internal static class WakeUpErrorMapper
{
    /// <summary>Délai conseillé à l'ordonnanceur avant de redemander un réveil non remis.</summary>
    public const int RetryAfterSeconds = 60;

    public static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.InvalidRequest => StatusCodes.Status400BadRequest,
        ErrorKind.UserNotFound => StatusCodes.Status404NotFound,
        ErrorKind.InvalidContact => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.UserServiceUnavailable
            or ErrorKind.ProviderUnavailable
            or ErrorKind.ChannelUnavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IResult ToProblem(DomainError error, HttpResponse response)
    {
        var status = StatusCodeFor(error.Kind);
        if (status == StatusCodes.Status503ServiceUnavailable)
        {
            response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return Results.Problem(detail: error.Message, statusCode: status, title: error.Kind.ToString());
    }
}
