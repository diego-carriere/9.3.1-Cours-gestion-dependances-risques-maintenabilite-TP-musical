using System.Globalization;
using ReveilMusical.Api.Contracts;
using ReveilMusical.Api.Errors;
using ReveilMusical.Application.WakeUp;

namespace ReveilMusical.Api.Endpoints;

/// <summary>
/// <c>POST /wake-ups</c>, l'appel que l'ordonnanceur déclenche à l'heure du réveil. Ne fait que
/// traduire : tout le travail est délégué à <see cref="ITriggerWakeUpUseCase"/>, injecté.
/// </summary>
/// <remarks>
/// Le réveil n'est pas lié à la connexion de l'ordonnanceur (<c>RequestAborted</c>) : un client qui
/// raccroche, sur un délai d'attente trop court par exemple, ne doit pas annuler un réveil en cours.
/// Seul l'arrêt de l'hôte l'interrompt. La durée reste bornée par les délais des fournisseurs et des
/// canaux (README, « Budget de latence »).
/// </remarks>
internal static class WakeUpEndpoint
{
    public static IEndpointRouteBuilder MapWakeUpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/wake-ups", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        WakeUpHttpRequest? body,
        ITriggerWakeUpUseCase useCase,
        IHostApplicationLifetime lifetime,
        HttpResponse response)
    {
        if (!WakeUpRequestParser.TryParse(body, out var request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var result = await useCase.ExecuteAsync(request!, lifetime.ApplicationStopping).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return WakeUpErrorMapper.ToProblem(result.Error, response);
        }

        var report = WakeUpResponseMapper.ToHttp(result.Value);
        if (report.Delivered)
        {
            return Results.Ok(report);
        }

        // Réveil non remis : l'opérateur est déjà alerté ; l'ordonnanceur peut retenter.
        response.Headers.RetryAfter = WakeUpErrorMapper.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        return Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
